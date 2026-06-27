import { Component, inject, signal } from '@angular/core';
import { WatchHistoryService } from '../../core/services/watch-history.service';
import { VideoCard } from '../../shared/video-card/video-card';
import { Button } from 'primeng/button';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-history',
  imports: [VideoCard, Button, TranslocoPipe],
  templateUrl: './history.html',
})
export class History {
  private readonly watchHistory = inject(WatchHistoryService);

  readonly videos = signal(this.watchHistory.getHistory());

  clearAll(): void {
    this.watchHistory.clear();
    this.videos.set([]);
  }
}
