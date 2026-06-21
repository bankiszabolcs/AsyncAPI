import { Component, computed, effect, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { timer, switchMap, map, catchError, of, takeWhile, tap } from 'rxjs';
import { TimeAgoPipe } from '../../shared/pipes/time-ago.pipe';
import { ViewCountPipe } from '../../shared/pipes/view-count.pipe';
import { VideoService } from '../../core/services/video.service';
import { VideoDetail } from '../../core/models/video-detail.model';
import { Video } from '../../core/models/video.model';
import { VideoPlayer } from '../../shared/video-player/video-player';
import { AuthService } from '../../core/auth/auth.service';
import { Comments } from '../../shared/comments/comments';
import { WatchHistoryService } from '../../core/services/watch-history.service';
import { VideoCard } from '../../shared/video-card/video-card';
import { SavedVideoService } from '../../core/services/saved-video.service';

// Amíg a videó feldolgozás alatt van, ennyi időközönként pollozzuk a státuszt
const POLL_INTERVAL_MS = 4000;

interface PollResult {
  video: VideoDetail | null;
  error: HttpErrorResponse | null;
  loading: boolean;
}

@Component({
  selector: 'app-watch',
  imports: [VideoPlayer, TimeAgoPipe, ViewCountPipe, Comments, VideoCard],
  providers: [SavedVideoService],
  templateUrl: './watch.html',
})
export class Watch {
  private readonly route            = inject(ActivatedRoute);
  private readonly videoService     = inject(VideoService);
  private readonly watchHistory     = inject(WatchHistoryService);
  private readonly savedVideoService = inject(SavedVideoService);
  readonly auth = inject(AuthService);

  readonly id = this.route.snapshot.paramMap.get('id')!;

  readonly relatedVideos = toSignal(this.videoService.getRelated(this.id));;

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

  private viewRecorded = false;

  onTimePlayed(seconds: number): void {
    if (this.viewRecorded) return;
    const duration = this.video()?.duration ?? 0;
    if (!duration) return;
    const threshold = Math.min(30, duration * 0.5);
    if (seconds >= threshold) {
      this.viewRecorded = true;
      this.videoService.recordView(this.id).subscribe();
    }
  }

  private historyRecorded = false;
  private readonly recordEffect = effect(() => {
    const v = this.video();
    if (v && this.isReady() && !this.historyRecorded) {
      this.historyRecorded = true;
      this.watchHistory.record(v);
    }
  });

  // Reakció számlálók — override-olja a video() értékét, ha a user éppen lájkolt
  private readonly reactionState = signal<{ likeCount: number; dislikeCount: number; userReaction: 1 | 2 | null } | null>(null);
  readonly likeCount    = computed(() => this.reactionState()?.likeCount    ?? this.video()?.likeCount    ?? 0);
  readonly dislikeCount = computed(() => this.reactionState()?.dislikeCount ?? this.video()?.dislikeCount ?? 0);
  readonly userReaction = computed(() => this.reactionState()?.userReaction ?? this.video()?.userReaction ?? null);

  react(type: 1 | 2): void {
    if (!this.auth.isLoggedIn()) {
      this.auth.login();
      return;
    }
    this.videoService.react(this.id, type).subscribe(state => this.reactionState.set(state));
  }

  readonly isSaved = signal<boolean>(false);

  private readonly savedEffect = effect(() => {
    const v = this.video();
    if (v && this.auth.isLoggedIn()) {
      this.savedVideoService.getStatus(this.id).subscribe(r => this.isSaved.set(r.isSaved));
    }
  }, { allowSignalWrites: true });

  toggleSave(): void {
    if (!this.auth.isLoggedIn()) {
      this.auth.login();
      return;
    }
    const op = this.isSaved()
      ? this.savedVideoService.unsave(this.id)
      : this.savedVideoService.save(this.id);
    op.subscribe(r => this.isSaved.set(r.isSaved));
  }

  readonly thumbnail = computed(() => {
    const thumbs = this.video()?.media.thumbnails ?? [];
    if (!thumbs.length) return null;
    return thumbs.reduce((best, t) => (t.width > best.width ? t : best)).url;
  });

  readonly streamUrl = computed(() => this.video()?.media.masterStream ?? '');

  readonly vttUrl = computed(() => this.video()?.media.preview ?? '');
}
