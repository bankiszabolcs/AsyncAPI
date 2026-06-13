import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { timer, switchMap, map, catchError, of, takeWhile, tap } from 'rxjs';
import { TimeAgoPipe } from '../../shared/pipes/time-ago.pipe';
import { VideoService } from '../../core/services/video.service';
import { VideoDetail } from '../../core/models/video-detail.model';
import { VideoPlayer } from '../../shared/video-player/video-player';

// Amíg a videó feldolgozás alatt van, ennyi időközönként pollozzuk a státuszt
const POLL_INTERVAL_MS = 4000;

interface PollResult {
  video: VideoDetail | null;
  error: HttpErrorResponse | null;
  loading: boolean;
}

@Component({
  selector: 'app-watch',
  imports: [VideoPlayer, TimeAgoPipe],
  templateUrl: './watch.html',
})
export class Watch {
  private readonly route = inject(ActivatedRoute);
  private readonly videoService = inject(VideoService);

  readonly id = this.route.snapshot.paramMap.get('id')!;

  // Az utolsó sikeres válasz — átmeneti hálózati hibánál ezt tartjuk meg
  private readonly lastVideo = signal<VideoDetail | null>(null);

  private readonly poll = toSignal(
    timer(0, POLL_INTERVAL_MS).pipe(
      switchMap(() =>
        this.videoService.getById(this.id).pipe(
          tap(video => this.lastVideo.set(video)),
          map((video): PollResult => ({ video, error: null, loading: false })),
          catchError((error: HttpErrorResponse) =>
            of<PollResult>({ video: null, error, loading: false })
          )
        )
      ),
      // amíg nincs kész/hiba, folytatjuk (true = az utolsó értéket is kiadja, mielőtt leáll)
      takeWhile(r => this.shouldKeepPolling(r), true)
    ),
    { initialValue: { video: null, error: null, loading: true } as PollResult }
  );

  // 404 → végleg leállunk; bármi más hiba → újrapróbálkozunk a következő tick-en
  private shouldKeepPolling(r: PollResult): boolean {
    if (r.error) return r.error.status !== 404;
    const s = r.video?.status?.toLowerCase();
    return s !== 'completed' && s !== 'failed';
  }

  // Az aktuális videó: friss válasz, vagy az utolsó ismert (hiba esetén)
  readonly video = computed(() => this.poll().video ?? this.lastVideo());

  readonly isLoading = computed(() => this.poll().loading && !this.lastVideo());

  readonly isNotFound = computed(() => {
    const err = this.poll().error;
    return err instanceof HttpErrorResponse && err.status === 404;
  });

  readonly status = computed(() => this.video()?.status?.toLowerCase() ?? '');

  readonly isReady = computed(() => this.status() === 'completed');

  readonly isProcessing = computed(() => {
    const s = this.status();
    return s === 'queued' || s === 'processing';
  });

  // Magyar címke a státuszhoz
  readonly statusLabel = computed(() => {
    switch (this.status()) {
      case 'queued':     return 'Sorban áll';
      case 'processing': return 'Feldolgozás folyamatban';
      case 'failed':     return 'A feldolgozás sikertelen';
      default:           return '';
    }
  });

  readonly thumbnail = computed(() => {
    const thumbs = this.video()?.media.thumbnails ?? [];
    if (!thumbs.length) return null;
    return thumbs.reduce((best, t) => (t.width > best.width ? t : best)).url;
  });

  readonly streamUrl = computed(() => this.video()?.media.masterStream ?? '');

  readonly vttUrl = computed(() => this.video()?.media.preview ?? '');
}
