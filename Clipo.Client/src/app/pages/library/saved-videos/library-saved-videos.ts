import { Component, inject, OnInit, signal } from '@angular/core';
import { catchError, of } from 'rxjs';
import { SavedVideoService } from '../../../core/services/saved-video.service';
import { SavedVideoItem } from '../../../core/models/saved-video.model';
import { VideoCard } from '../../../shared/video-card/video-card';

@Component({
  selector: 'app-library-saved-videos',
  imports: [VideoCard],
  providers: [SavedVideoService],
  templateUrl: './library-saved-videos.html',
})
export class LibrarySavedVideos implements OnInit {
  private readonly savedVideoService = inject(SavedVideoService);

  readonly videos    = signal<SavedVideoItem[]>([]);
  readonly isLoading = signal(true);

  ngOnInit(): void {
    this.savedVideoService.getSavedVideos()
      .pipe(catchError(() => of([] as SavedVideoItem[])))
      .subscribe(list => {
        this.videos.set(list);
        this.isLoading.set(false);
      });
  }
}
