import { Component, inject, signal, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { VideoService } from '../../core/services/video.service';
import { VideoCard } from '../../shared/video-card/video-card';
import { PlaylistList } from './playlist-list/playlist-list';

type Tab = 'videos' | 'playlists' | 'about';

@Component({
  selector: 'app-channel',
  imports: [RouterLink, VideoCard, PlaylistList],
  templateUrl: './channel.html',
})
export class Channel {
  readonly auth = inject(AuthService);
  private readonly videoService = inject(VideoService);

  readonly activeTab = signal<Tab>('videos');

  private readonly videosResult = toSignal(
    this.videoService.getMyVideos().pipe(
      catchError(() => of(null))
    ),
    { initialValue: undefined }
  );

  readonly isLoading = computed(() => this.videosResult() === undefined);
  readonly videos = computed(() => this.videosResult() ?? []);
}
