import { Component, computed, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { rxResource } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { VideoService } from '../../core/services/video.service';
import { VideoPlayer } from '../../shared/video-player/video-player';

@Component({
  selector: 'app-watch',
  imports: [VideoPlayer, DatePipe],
  templateUrl: './watch.html',
})
export class Watch {
  private readonly route = inject(ActivatedRoute);
  private readonly videoService = inject(VideoService);

  readonly id = this.route.snapshot.paramMap.get('id')!;

  readonly videoResource = rxResource({
    stream: () => this.videoService.getById(this.id),
  });

  readonly isNotFound = computed(() => {
    const err = this.videoResource.error();
    return err instanceof HttpErrorResponse && err.status === 404;
  });

  readonly isReady = computed(() => {
    const v = this.videoResource.value();
    return typeof v?.status === 'string' && v.status.toLowerCase() === 'completed';
  });

  readonly isProcessing = computed(() => {
    const v = this.videoResource.value();
    if (!v || typeof v.status !== 'string') return false;
    const s = v.status.toLowerCase();
    return s === 'queued' || s === 'processing';
  });

  readonly thumbnail = computed(() => {
    const thumbs = this.videoResource.value()?.media.thumbnails ?? [];
    if (!thumbs.length) return null;
    return thumbs.reduce((best, t) => (t.width > best.width ? t : best)).url;
  });

  // Legjobb minőségű stream kiválasztása
  readonly streamUrl = computed(() => {
    const streams = this.videoResource.value()?.media.streams ?? [];
    if (!streams.length) return '';
    for (const q of ['1080p', '720p', '480p']) {
      const match = streams.find(s => s.quality === q);
      if (match) return match.url;
    }
    return streams[0].url;
  });

  readonly vttUrl = computed(() =>
    this.videoResource.value()?.media.preview ?? ''
  );
}
