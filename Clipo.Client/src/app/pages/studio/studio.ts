import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { catchError, of } from 'rxjs';
import { Button } from 'primeng/button';
import { Select } from 'primeng/select';
import { VideoService } from '../../core/services/video.service';
import { MyVideo } from '../../core/models/my-video.model';
import { Visibility, visibilityOption, VISIBILITY_OPTIONS } from '../../core/models/visibility.model';
import { ProcessingStatus } from '../../core/models/processing-status.model';

interface Stat {
  label: string;
  value: string | number;
  icon: string;
}

@Component({
  selector: 'app-studio',
  imports: [RouterLink, FormsModule, Button, Select],
  templateUrl: './studio.html',
  styles: ``,
})
export class Studio {
  private readonly videoService = inject(VideoService);

  // null = még tölt
  readonly videos = signal<MyVideo[] | null>(null);
  readonly isLoading = computed(() => this.videos() === null);

  readonly visibilityOptions = [...VISIBILITY_OPTIONS];

  // Inline szerkesztés állapota
  readonly editingId = signal<string | null>(null);
  readonly editTitle = signal('');
  readonly editDescription = signal('');
  readonly editVisibilityId = signal<number>(Visibility.Private);
  readonly saving = signal(false);

  readonly stats = computed<Stat[]>(() => {
    const list = this.videos() ?? [];
    const published = list.filter(
      v => v.statusId === ProcessingStatus.Completed && v.visibilityId === Visibility.Public,
    ).length;
    return [
      { label: 'Total videos', value: list.length, icon: 'pi-video' },
      { label: 'Published',    value: published,   icon: 'pi-globe' },
      { label: 'Drafts',       value: list.length - published, icon: 'pi-file-edit' },
    ];
  });

  constructor() {
    this.videoService.getMyVideos()
      .pipe(catchError(() => of([] as MyVideo[])))
      .subscribe(list => this.videos.set(list));
  }

  thumbnail(v: MyVideo): string | null {
    const thumbs = v.media.thumbnails;
    if (!thumbs?.length) return null;
    return thumbs.reduce((best, t) => (t.width > best.width ? t : best)).url;
  }

  visibility(v: MyVideo) {
    return visibilityOption(v.visibilityId);
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
    }).subscribe({
      next: res => {
        this.videos.update(list =>
          (list ?? []).map(x =>
            x.id === res.id
              ? { ...x, title: res.title, description: res.description, visibilityId: res.visibilityId }
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
}
