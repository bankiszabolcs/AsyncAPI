import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Skeleton } from 'primeng/skeleton';
import { CategoryService } from '../../core/services/category.service';
import { VideoService } from '../../core/services/video.service';
import { VideoCard } from '../../shared/video-card/video-card';
import { Category } from '../../core/models/category.model';

const INITIAL_VISIBLE = 4;
const EXPAND_STEP = 5;

@Component({
  selector: 'app-explore',
  imports: [VideoCard, Skeleton],
  templateUrl: './explore.html',
  styles: ``,
})
export class Explore {
  private readonly categoryService = inject(CategoryService);
  private readonly videoService    = inject(VideoService);

  readonly selectedId  = signal<string | null>(null);
  readonly visibleCount = signal(INITIAL_VISIBLE);

  readonly categoriesResource = rxResource({
    stream: () => this.categoryService.getAll(),
  });

  readonly visibleCategories = computed<Category[]>(() => {
    const all = this.categoriesResource.value() ?? [];
    return all.slice(0, this.visibleCount());
  });

  readonly hasMore = computed(() => {
    const all = this.categoriesResource.value() ?? [];
    return this.visibleCount() < all.length;
  });

  readonly videosResource = rxResource({
    params: () => this.selectedId(),
    stream: ({ params: categoryId }) => this.videoService.getAll(1, 20, categoryId),
  });

  selectCategory(id: string | null): void {
    this.selectedId.set(id);
  }

  showMore(): void {
    this.visibleCount.update(n => n + EXPAND_STEP);
  }
}
