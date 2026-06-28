import { Component, computed, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { timer, switchMap, map, catchError, of, takeWhile, tap } from 'rxjs';
import { TranslocoService, TranslocoPipe } from '@jsverse/transloco';
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
import { WatchLaterService } from '../../core/services/watch-later.service';
import { SubscriptionService } from '../../core/services/subscription.service';

const POLL_INTERVAL_MS = 4000;

interface PollResult {
  video: VideoDetail | null;
  error: HttpErrorResponse | null;
  loading: boolean;
}

@Component({
  selector: 'app-watch',
  imports: [VideoPlayer, TimeAgoPipe, ViewCountPipe, Comments, VideoCard, RouterLink, TranslocoPipe],
  providers: [SavedVideoService, WatchLaterService],
  templateUrl: './watch.html',
})
export class Watch {
  private readonly route            = inject(ActivatedRoute);
  private readonly videoService     = inject(VideoService);
  private readonly watchHistory     = inject(WatchHistoryService);
  private readonly savedVideoService  = inject(SavedVideoService);
  private readonly watchLaterService  = inject(WatchLaterService);
  private readonly subscriptionSvc  = inject(SubscriptionService);
  private readonly transloco        = inject(TranslocoService);
  readonly auth = inject(AuthService);

  readonly id = this.route.snapshot.paramMap.get('id')!;

  readonly highlightCommentId = toSignal(
    this.route.queryParamMap.pipe(map(p => p.get('highlight')))
  );

  readonly relatedVideos = toSignal(this.videoService.getRelated(this.id));;

  private readonly lastVideo = signal<VideoDetail | null>(null);

  private readonly activeLang = toSignal(this.transloco.langChanges$, {
    initialValue: this.transloco.getActiveLang(),
  });

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
      takeWhile(r => this.shouldKeepPolling(r), true)
    ),
    { initialValue: { video: null, error: null, loading: true } as PollResult }
  );

  private shouldKeepPolling(r: PollResult): boolean {
    if (r.error) return r.error.status !== 404;
    const s = r.video?.status?.toLowerCase();
    return s !== 'completed' && s !== 'failed';
  }

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

  readonly statusLabel = computed(() => {
    void this.activeLang();
    switch (this.status()) {
      case 'queued':     return this.transloco.translate('watch.status.queued');
      case 'processing': return this.transloco.translate('watch.status.processing');
      case 'failed':     return this.transloco.translate('watch.status.failed');
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

  private readonly subState = signal<{ count: number; subscribed: boolean | null } | null>(null);
  readonly subscriberCount  = computed(() => this.subState()?.count ?? null);
  readonly isSubscribed     = computed(() => this.subState()?.subscribed ?? null);
  readonly isSubscribing    = signal(false);
  readonly isOwnVideo = computed(() => this.auth.profile()?.id === this.video()?.author.id);

  private channelLoaded = false;
  private readonly channelEffect = effect(() => {
    const v = this.video();
    if (!v || this.channelLoaded) return;
    this.channelLoaded = true;
    this.subscriptionSvc.getChannel(v.author.id).subscribe(ch =>
      this.subState.set({ count: ch.subscriberCount, subscribed: ch.isSubscribed })
    );
  }, { allowSignalWrites: true });

  toggleSubscribe(): void {
    if (!this.auth.isLoggedIn()) { this.auth.login(); return; }
    const v = this.video();
    if (!v || this.isSubscribing()) return;
    this.isSubscribing.set(true);
    const op = this.isSubscribed()
      ? this.subscriptionSvc.unsubscribe(v.author.id)
      : this.subscriptionSvc.subscribe(v.author.id);
    op.subscribe({
      next:  r  => { this.subState.set({ count: r.subscriberCount, subscribed: r.isSubscribed }); this.isSubscribing.set(false); },
      error: () => this.isSubscribing.set(false),
    });
  }

  toggleSave(): void {
    if (!this.auth.isLoggedIn()) { this.auth.login(); return; }
    const op = this.isSaved()
      ? this.savedVideoService.unsave(this.id)
      : this.savedVideoService.save(this.id);
    op.subscribe(r => this.isSaved.set(r.isSaved));
  }

  readonly isWatchLater = signal<boolean>(false);

  private readonly watchLaterEffect = effect(() => {
    const v = this.video();
    if (v && this.auth.isLoggedIn()) {
      this.watchLaterService.getStatus(this.id).subscribe(r => this.isWatchLater.set(r.isAdded));
    }
  }, { allowSignalWrites: true });

  toggleWatchLater(): void {
    if (!this.auth.isLoggedIn()) { this.auth.login(); return; }
    const op = this.isWatchLater()
      ? this.watchLaterService.remove(this.id)
      : this.watchLaterService.add(this.id);
    op.subscribe(r => this.isWatchLater.set(r.isAdded));
  }

  readonly thumbnail = computed(() => {
    const thumbs = this.video()?.media.thumbnails ?? [];
    if (!thumbs.length) return null;
    return thumbs.reduce((best, t) => (t.width > best.width ? t : best)).url;
  });

  readonly streamUrl = computed(() => this.video()?.media.masterStream ?? '');
  readonly vttUrl = computed(() => this.video()?.media.preview ?? '');

  readonly shareCopied = signal(false);

  share(): void {
    const url = window.location.href;
    const title = this.video()?.title ?? '';
    if (navigator.share) {
      navigator.share({ title, url }).catch(() => {});
    } else {
      navigator.clipboard.writeText(url).then(() => {
        this.shareCopied.set(true);
        setTimeout(() => this.shareCopied.set(false), 2000);
      });
    }
  }
}
