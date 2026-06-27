import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { Button } from 'primeng/button';
import { Select } from 'primeng/select';
import { BadgeModule } from 'primeng/badge';
import { TranslocoService, TranslocoPipe } from '@jsverse/transloco';
import { VideoService } from '../../core/services/video.service';
import { CategoryService } from '../../core/services/category.service';
import { MyVideo } from '../../core/models/my-video.model';
import { Visibility, VISIBILITY_OPTIONS } from '../../core/models/visibility.model';
import { ProcessingStatus } from '../../core/models/processing-status.model';
import { Category } from '../../core/models/category.model';

interface Stat {
  label: string;
  value: string | number;
  icon: string;
}

@Component({
  selector: 'app-studio',
  imports: [RouterLink, FormsModule, Button, Select, BadgeModule, TranslocoPipe],
  templateUrl: './studio.html',
  styles: ``,
})
export class Studio {
  private readonly videoService    = inject(VideoService);
  private readonly categoryService = inject(CategoryService);
  private readonly transloco       = inject(TranslocoService);

  private readonly activeLang = toSignal(this.transloco.langChanges$, {
    initialValue: this.transloco.getActiveLang(),
  });

  readonly videos = signal<MyVideo[] | null>(null);
  readonly isLoading = computed(() => this.videos() === null);

  readonly categories = signal<Category[]>([]);

  readonly visibilityOptions = computed(() => {
    void this.activeLang();
    return VISIBILITY_OPTIONS.map(o => ({
      value: o.value,
      icon: o.icon,
      label: this.transloco.translate(`visibility.${o.translationKey}.label`),
    }));
  });

  readonly categoryOptions = computed(() => {
    void this.activeLang();
    return [
      { value: null, label: this.transloco.translate('studio.categoryNone') },
      ...this.categories().map(c => ({ value: c.id, label: c.title })),
    ];
  });

  readonly editingId       = signal<string | null>(null);
  readonly editTitle       = signal('');
  readonly editDescription = signal('');
  readonly editVisibilityId  = signal<number>(Visibility.Private);
  readonly editCategoryId  = signal<string | null>(null);
  readonly editTags        = signal<string[]>([]);
  readonly showTagInput = signal(false);
  readonly saving = signal(false);

  readonly tagBadgeStyle = { padding: '0.35rem 1rem' };

  readonly stats = computed<Stat[]>(() => {
    void this.activeLang();
    const list = this.videos() ?? [];
    const published = list.filter(
      v => v.statusId === ProcessingStatus.Completed && v.visibilityId === Visibility.Public,
    ).length;
    return [
      { label: this.transloco.translate('studio.stats.total'),     value: list.length,             icon: 'pi-video' },
      { label: this.transloco.translate('studio.stats.published'), value: published,               icon: 'pi-globe' },
      { label: this.transloco.translate('studio.stats.drafts'),    value: list.length - published, icon: 'pi-file-edit' },
    ];
  });

  constructor() {
    this.videoService.getMyVideos()
      .pipe(catchError(() => of([] as MyVideo[])))
      .subscribe(list => this.videos.set(list));

    this.categoryService.getAll()
      .pipe(catchError(() => of([] as Category[])))
      .subscribe(list => this.categories.set(list));
  }

  thumbnail(v: MyVideo): string | null {
    const thumbs = v.media.thumbnails;
    if (!thumbs?.length) return null;
    return thumbs.reduce((best, t) => (t.width > best.width ? t : best)).url;
  }

  isCompleted(v: MyVideo): boolean {
    return v.statusId === ProcessingStatus.Completed;
  }

  formatDuration(seconds: number | null): string {
    if (!seconds) return '';
    const m = Math.floor(seconds / 60);
    const s = Math.floor(seconds % 60);
    return `${m}:${s.toString().padStart(2, '0')}`;
  }

  startEdit(v: MyVideo): void {
    this.editingId.set(v.id);
    this.editTitle.set(v.title);
    this.editDescription.set(v.description ?? '');
    this.editVisibilityId.set(v.visibilityId);
    this.editCategoryId.set(v.categoryId);
    this.editTags.set([...v.tags]);
    this.showTagInput.set(false);
  }

  openTagInput(): void {
    this.showTagInput.set(true);
    setTimeout(() => (document.querySelector('[data-tag-input]') as HTMLInputElement)?.focus());
  }

  cancelEdit(): void {
    if (this.saving()) return;
    this.editingId.set(null);
  }

  saveEdit(v: MyVideo): void {
    const title = this.editTitle().trim();
    if (!title) return;

    this.saving.set(true);
    this.videoService.updateVideo(v.id, {
      title,
      description: this.editDescription().trim() || null,
      visibilityId: this.editVisibilityId(),
      categoryId: this.editCategoryId(),
      tags: this.editTags(),
    }).subscribe({
      next: res => {
        this.videos.update(list =>
          (list ?? []).map(x =>
            x.id === res.id
              ? { ...x, title: res.title, description: res.description, visibilityId: res.visibilityId, categoryId: res.categoryId, tags: res.tags }
              : x,
          ),
        );
        this.saving.set(false);
        this.editingId.set(null);
      },
      error: () => {
        this.saving.set(false);
      },
    });
  }

  removeTag(tag: string): void {
    this.editTags.update(tags => tags.filter(t => t !== tag));
  }

  onTagKeydown(event: KeyboardEvent, input: HTMLInputElement): void {
    if (event.key === ' ' || event.key === 'Enter') {
      event.preventDefault();
      this.addTagValue(input);
    } else if (event.key === 'Escape') {
      input.value = '';
      this.showTagInput.set(false);
    } else if (event.key === 'Backspace' && input.value === '' && this.editTags().length > 0) {
      this.editTags.update(tags => tags.slice(0, -1));
    }
  }

  onTagBlur(input: HTMLInputElement): void {
    this.addTagValue(input);
    this.showTagInput.set(false);
  }

  private addTagValue(input: HTMLInputElement): void {
    const value = input.value.trim().toLowerCase();
    if (value && value.length <= 20 && !this.editTags().includes(value) && this.editTags().length < 10) {
      this.editTags.update(tags => [...tags, value]);
    }
    input.value = '';
  }
}
