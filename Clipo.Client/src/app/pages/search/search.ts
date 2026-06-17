import { Component, inject } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { map, distinctUntilChanged, switchMap } from 'rxjs';
import { DataView } from 'primeng/dataview';
import { Skeleton } from 'primeng/skeleton';
import { VideoService } from '../../core/services/video.service';
import { VideoCard } from '../../shared/video-card/video-card';
import { Video } from '../../core/models/video.model';

@Component({
  selector: 'app-search',
  imports: [DataView, VideoCard, Skeleton],
  templateUrl: './search.html',
})
export class Search {
  private readonly route = inject(ActivatedRoute);
  private readonly videoService = inject(VideoService);

  private readonly query$ = this.route.queryParamMap.pipe(
    map(params => params.get('q') ?? ''),
    distinctUntilChanged(),
  );

  readonly searchQuery = toSignal(this.query$, { initialValue: '' });

  readonly videosResource = rxResource<Video[], string>({
    stream: () => this.query$.pipe(
      switchMap(q => this.videoService.search(q)),
    ),
  });
}
