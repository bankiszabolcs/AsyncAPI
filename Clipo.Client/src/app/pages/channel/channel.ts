import { Component, inject, signal, computed } from '@angular/core';
import { RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { VideoService } from '../../core/services/video.service';
import { MyVideo } from '../../core/models/my-video.model';
import { TimeAgoPipe } from '../../shared/pipes/time-ago.pipe';

type Tab = 'videos' | 'playlists' | 'about';

@Component({
  selector: 'app-channel',
  imports: [RouterLink, TimeAgoPipe],
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

  readonly statusLabel: Record<number, string> = {
    1: 'Sorban áll',
    2: 'Feldolgozás',
    3: 'Kész',
    4: 'Sikertelen',
  };

  readonly statusClass: Record<number, string> = {
    1: 'bg-gray-500/20 text-gray-400',
    2: 'bg-blue-500/20 text-blue-400',
    3: 'bg-green-500/20 text-green-400',
    4: 'bg-red-500/20 text-red-400',
  };

  thumbnail(video: MyVideo): string | null {
    const thumbs = video.media.thumbnails;
    if (!thumbs?.length) return null;
    const sorted = [...thumbs].sort((a, b) => b.width - a.width);
    return sorted[0].url;
  }

  fmtDuration(seconds: number): string {
    if (!seconds) return '';
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = Math.floor(seconds % 60);
    return h > 0
      ? `${h}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`
      : `${m}:${s.toString().padStart(2, '0')}`;
  }
}
