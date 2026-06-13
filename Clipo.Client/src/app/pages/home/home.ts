import { Component, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { DataView } from 'primeng/dataview';
import { Skeleton } from 'primeng/skeleton';
import { VideoService } from '../../core/services/video.service';
import { VideoCard } from '../../shared/video-card/video-card';

@Component({
  selector: 'app-home',
  imports: [DataView, VideoCard, Skeleton],
  templateUrl: './home.html',
})
export class Home {
  private readonly videoService = inject(VideoService);

  readonly videosResource = rxResource({
    stream: () => this.videoService.getAll(),
  });
}
