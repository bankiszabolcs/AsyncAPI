import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, of, switchMap } from 'rxjs';
import { PlaylistService } from '../../../core/services/playlist.service';
import { PlaylistDetail } from '../../../core/models/playlist.model';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-library-playlist-detail',
  imports: [RouterLink, TranslocoPipe],
  providers: [PlaylistService],
  templateUrl: './library-playlist-detail.html',
})
export class LibraryPlaylistDetail implements OnInit {
  private readonly route           = inject(ActivatedRoute);
  private readonly playlistService = inject(PlaylistService);

  readonly playlist  = signal<PlaylistDetail | null>(null);
  readonly isLoading = signal(true);

  ngOnInit(): void {
    this.route.params.pipe(
      switchMap(params =>
        this.playlistService.getPlaylist(params['id']).pipe(catchError(() => of(null)))
      )
    ).subscribe(p => {
      this.playlist.set(p);
      this.isLoading.set(false);
    });
  }

  thumbnail(video: { thumbnails: { width: number; url: string }[] }): string | null {
    if (!video.thumbnails?.length) return null;
    return video.thumbnails.reduce((best, t) => t.width > best.width ? t : best).url;
  }

  formatDuration(seconds: number): string {
    if (!seconds) return '';
    const m = Math.floor(seconds / 60);
    const s = Math.floor(seconds % 60);
    return `${m}:${s.toString().padStart(2, '0')}`;
  }
}
