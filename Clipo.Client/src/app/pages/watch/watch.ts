import { Component, computed, effect, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { HttpErrorResponse } from '@angular/common/http';
import { timer, switchMap, map, catchError, of, takeWhile, tap } from 'rxjs';
import { TranslocoService, TranslocoPipe } from '@jsverse/transloco';
import { Dialog } from 'primeng/dialog';
import { Divider } from 'primeng/divider';
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
  imports: [VideoPlayer, TimeAgoPipe, ViewCountPipe, Comments, VideoCard, RouterLink, TranslocoPipe, Dialog, Divider],
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

  readonly shareVisible = signal(false);
  readonly copyDone = signal(false);
  readonly currentUrl = window.location.href;

  readonly sharePlatforms = [
    {
      id: 'x',
      label: 'X',
      color: '#141414',
      path: 'M18.244 2.25h3.308l-7.227 8.26 8.502 11.24H16.17l-4.714-6.231-5.401 6.231H2.747l7.73-8.835L1.254 2.25H8.08l4.713 6.231zm-1.161 17.52h1.833L7.084 4.126H5.117z',
    },
    {
      id: 'facebook',
      label: 'Facebook',
      color: '#1877F2',
      path: 'M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z',
    },
    {
      id: 'whatsapp',
      label: 'WhatsApp',
      color: '#25D366',
      path: 'M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z',
    },
    {
      id: 'email',
      label: 'Email',
      color: '#6B7280',
      path: 'M22 6c0-1.1-.9-2-2-2H4c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V6zm-2 0l-8 5-8-5h16zm0 12H4V8l8 5 8-5v10z',
    },
  ];

  shareOn(platform: string): void {
    const url   = encodeURIComponent(window.location.href);
    const title = encodeURIComponent(this.video()?.title ?? '');
    const links: Record<string, string> = {
      x:        `https://x.com/intent/tweet?url=${url}&text=${title}`,
      facebook: `https://www.facebook.com/sharer/sharer.php?u=${url}`,
      whatsapp: `https://wa.me/?text=${title}%20${url}`,
      telegram: `https://t.me/share/url?url=${url}&text=${title}`,
      email:    `mailto:?subject=${title}&body=${url}`,
    };
    const link = links[platform];
    if (link) window.open(link, '_blank', 'noopener,noreferrer');
  }

  copyLink(): void {
    const url = window.location.href;
    const done = () => {
      this.copyDone.set(true);
      setTimeout(() => this.copyDone.set(false), 2000);
    };
    if (navigator.clipboard) {
      navigator.clipboard.writeText(url).then(done).catch(() => { this.legacyCopy(url); done(); });
    } else {
      this.legacyCopy(url);
      done();
    }
  }

  private legacyCopy(text: string): void {
    const el = document.createElement('textarea');
    el.value = text;
    el.style.cssText = 'position:fixed;opacity:0;top:0;left:0';
    document.body.appendChild(el);
    el.focus();
    el.select();
    document.execCommand('copy');
    document.body.removeChild(el);
  }
}
