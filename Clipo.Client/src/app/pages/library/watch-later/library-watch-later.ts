import { Component, inject, OnInit, signal } from '@angular/core';
import { catchError, of } from 'rxjs';
import { WatchLaterService } from '../../../core/services/watch-later.service';
import { WatchLaterItem } from '../../../core/models/watch-later.model';
import { VideoCard } from '../../../shared/video-card/video-card';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-library-watch-later',
  imports: [VideoCard, TranslocoPipe],
  providers: [WatchLaterService],
  templateUrl: './library-watch-later.html',
})
export class LibraryWatchLater implements OnInit {
  private readonly watchLaterService = inject(WatchLaterService);

  readonly videos    = signal<WatchLaterItem[]>([]);
  readonly isLoading = signal(true);

  ngOnInit(): void {
    this.watchLaterService.getWatchLater()
      .pipe(catchError(() => of([] as WatchLaterItem[])))
      .subscribe(list => {
        this.videos.set(list);
        this.isLoading.set(false);
      });
  }
}
