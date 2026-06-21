import { Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { SubscriptionService } from '../../core/services/subscription.service';
import { VideoService } from '../../core/services/video.service';
import { VideoCard } from '../../shared/video-card/video-card';
import { ViewCountPipe } from '../../shared/pipes/view-count.pipe';

@Component({
  selector: 'app-subscriptions',
  imports: [RouterLink, VideoCard, ViewCountPipe],
  templateUrl: './subscriptions.html',
})
export class Subscriptions {
  private readonly subSvc   = inject(SubscriptionService);
  private readonly videoSvc = inject(VideoService);

  readonly channelsResource = rxResource({
    stream: () => this.subSvc.getMySubscriptions(),
  });

  readonly channels = computed(() => this.channelsResource.value() ?? []);

  readonly selectedChannelId = signal<string | null>(null);

  readonly videosResource = rxResource({
    params: () => this.selectedChannelId(),
    stream: ({ params: channelId }) => this.videoSvc.getAll(1, 20, null, channelId),
  });

  readonly videos  = computed(() => this.videosResource.value() ?? []);

  selectChannel(id: string): void {
    this.selectedChannelId.set(this.selectedChannelId() === id ? null : id);
  }
}
