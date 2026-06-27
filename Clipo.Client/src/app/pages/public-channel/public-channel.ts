import { Component, computed, effect, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { rxResource } from '@angular/core/rxjs-interop';
import { AuthService } from '../../core/auth/auth.service';
import { SubscriptionService } from '../../core/services/subscription.service';
import { VideoService } from '../../core/services/video.service';
import { VideoCard } from '../../shared/video-card/video-card';
import { ViewCountPipe } from '../../shared/pipes/view-count.pipe';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-public-channel',
  imports: [VideoCard, ViewCountPipe, TranslocoPipe],
  providers: [VideoService],
  templateUrl: './public-channel.html',
})
export class PublicChannel {
  readonly auth              = inject(AuthService);
  private readonly route     = inject(ActivatedRoute);
  private readonly subSvc    = inject(SubscriptionService);
  private readonly videoSvc  = inject(VideoService);

  readonly channelId = this.route.snapshot.paramMap.get('id')!;

  readonly channelResource = rxResource({
    stream: () => this.subSvc.getChannel(this.channelId),
  });

  readonly videosResource = rxResource({
    stream: () => this.videoSvc.getAll(1, 30, null, this.channelId),
  });

  readonly channel    = computed(() => this.channelResource.value());
  readonly videos     = computed(() => this.videosResource.value() ?? []);
  readonly isLoading  = computed(() => this.channelResource.isLoading());
  readonly isOwnChannel = computed(() => this.auth.profile()?.id === this.channelId);

  private readonly subState = signal<{ count: number; subscribed: boolean | null } | null>(null);

  readonly subscriberCount = computed(() =>
    this.subState()?.count ?? this.channel()?.subscriberCount ?? null
  );
  readonly isSubscribed = computed(() =>
    this.subState()?.subscribed ?? this.channel()?.isSubscribed ?? null
  );
  readonly isSubscribing = signal(false);

  private readonly syncEffect = effect(() => {
    const ch = this.channel();
    if (ch) this.subState.set({ count: ch.subscriberCount, subscribed: ch.isSubscribed });
  }, { allowSignalWrites: true });

  toggleSubscribe(): void {
    if (!this.auth.isLoggedIn()) { this.auth.login(); return; }
    if (this.isSubscribing()) return;
    this.isSubscribing.set(true);

    const op = this.isSubscribed()
      ? this.subSvc.unsubscribe(this.channelId)
      : this.subSvc.subscribe(this.channelId);

    op.subscribe({
      next: r  => { this.subState.set({ count: r.subscriberCount, subscribed: r.isSubscribed }); this.isSubscribing.set(false); },
      error: () => this.isSubscribing.set(false),
    });
  }
}
