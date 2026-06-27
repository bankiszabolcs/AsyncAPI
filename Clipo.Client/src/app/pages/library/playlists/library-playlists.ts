import { Component, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, of } from 'rxjs';
import { PlaylistService } from '../../../core/services/playlist.service';
import { Playlist } from '../../../core/models/playlist.model';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-library-playlists',
  imports: [TranslocoPipe],
  providers: [PlaylistService],
  templateUrl: './library-playlists.html',
})
export class LibraryPlaylists implements OnInit {
  private readonly playlistService = inject(PlaylistService);
  private readonly router          = inject(Router);

  readonly playlists = signal<Playlist[]>([]);
  readonly isLoading = signal(true);

  ngOnInit(): void {
    this.playlistService.getMyPlaylists()
      .pipe(catchError(() => of([] as Playlist[])))
      .subscribe(list => {
        this.playlists.set(list);
        this.isLoading.set(false);
      });
  }

  openPlaylist(id: string): void {
    this.router.navigate(['/library/playlists', id]);
  }

  formatVideoCount(n: number): string {
    return n === 1 ? '1 videó' : `${n} videó`;
  }
}
