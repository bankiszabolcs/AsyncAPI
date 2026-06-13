import {
  Component, computed, effect, ElementRef, inject,
  input, OnDestroy, signal, viewChild,
} from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { HttpClient } from '@angular/common/http';
import Hls from 'hls.js';
import { parseVtt, SpriteFrame } from './vtt-parser';

@Component({
  selector: 'app-video-player',
  imports: [],
  templateUrl: './video-player.html',
})
export class VideoPlayer implements OnDestroy {
  readonly src = input.required<string>();
  readonly title = input('');
  readonly thumbnailsUrl = input('');

  private readonly sanitizer = inject(DomSanitizer);
  private readonly http = inject(HttpClient);
  private readonly videoEl = viewChild<ElementRef<HTMLVideoElement>>('videoEl');
  private hls: Hls | null = null;
  private hideControlsTimer: ReturnType<typeof setTimeout> | null = null;

  // Lejátszó állapot
  readonly isPlaying = signal(false);
  readonly currentTime = signal(0);
  readonly duration = signal(0);
  readonly buffered = signal(0);
  readonly volume = signal(1);
  readonly isMuted = signal(false);
  readonly isFullscreen = signal(false);
  readonly showControls = signal(true);

  // Sprite seek preview
  private readonly frames = signal<SpriteFrame[]>([]);
  readonly hoverRatio = signal<number | null>(null);

  readonly progress = computed(() =>
    this.duration() > 0 ? (this.currentTime() / this.duration()) * 100 : 0
  );

  readonly currentFormatted = computed(() => this.fmtTime(this.currentTime()));
  readonly durationFormatted = computed(() => this.fmtTime(this.duration()));
  readonly hoverTime = computed(() => {
    const r = this.hoverRatio();
    return r !== null ? this.fmtTime(r * this.duration()) : '';
  });

  readonly spriteFrame = computed((): SpriteFrame | null => {
    const r = this.hoverRatio();
    const frames = this.frames();
    if (r === null || !frames.length) return null;
    const t = r * this.duration();
    return frames.find(f => t >= f.start && t < f.end) ?? frames[frames.length - 1];
  });

  // bypassSecurityTrustStyle kell, mert a sprite.jpg MinIO URL-en van
  readonly safeSpriteUrl = computed(() => {
    const f = this.spriteFrame();
    if (!f) return null;
    return this.sanitizer.bypassSecurityTrustStyle(`url("${f.url}")`);
  });

  constructor() {
    // HLS inicializálás — a viewChild csak nézet init után lesz elérhető,
    // ezért a dependencia mindkettőt figyeli: src() és videoEl()
    effect(() => {
      const src = this.src();
      const el = this.videoEl()?.nativeElement;
      if (src && el) this.initHls(src, el);
    });

    effect(() => {
      const url = this.thumbnailsUrl();
      if (url) this.loadVtt(url);
    });
  }

  private initHls(src: string, el: HTMLVideoElement): void {
    this.hls?.destroy();

    if (Hls.isSupported()) {
      this.hls = new Hls();
      this.hls.loadSource(src);
      this.hls.attachMedia(el);
    } else if (el.canPlayType('application/vnd.apple.mpegurl')) {
      el.src = src;
    }
  }

  private loadVtt(url: string): void {
    this.http.get(url, { responseType: 'text' }).subscribe({
      next: text => { this.frames.set(parseVtt(text, url)); },
    });
  }

  // Video element eseménykezelők
  onTimeUpdate(e: Event): void {
    this.currentTime.set((e.target as HTMLVideoElement).currentTime);
  }

  onDurationChange(e: Event): void {
    this.duration.set((e.target as HTMLVideoElement).duration);
  }

  onProgress(e: Event): void {
    const v = e.target as HTMLVideoElement;
    if (v.buffered.length && v.duration) {
      this.buffered.set((v.buffered.end(v.buffered.length - 1) / v.duration) * 100);
    }
  }

  onPlay(): void { this.isPlaying.set(true); }
  onPause(): void { this.isPlaying.set(false); }
  onEnded(): void { this.isPlaying.set(false); }

  // Vezérlők
  togglePlay(): void {
    const el = this.videoEl()?.nativeElement;
    if (!el) return;
    el.paused ? el.play() : el.pause();
  }

  seek(e: MouseEvent): void {
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    const el = this.videoEl()?.nativeElement;
    if (!el) return;
    el.currentTime = ((e.clientX - rect.left) / rect.width) * this.duration();
  }

  onSeekHover(e: MouseEvent): void {
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    this.hoverRatio.set((e.clientX - rect.left) / rect.width);
  }

  onSeekLeave(): void {
    this.hoverRatio.set(null);
  }

  toggleMute(): void {
    const el = this.videoEl()?.nativeElement;
    if (!el) return;
    el.muted = !el.muted;
    this.isMuted.set(el.muted);
  }

  onVolumeChange(e: Event): void {
    const v = +(e.target as HTMLInputElement).value;
    const el = this.videoEl()?.nativeElement;
    if (!el) return;
    el.volume = v;
    el.muted = v === 0;
    this.volume.set(v);
    this.isMuted.set(v === 0);
  }

  toggleFullscreen(): void {
    const container = this.videoEl()?.nativeElement.closest('.player-root') as HTMLElement;
    if (!container) return;
    if (!document.fullscreenElement) {
      container.requestFullscreen();
      this.isFullscreen.set(true);
    } else {
      document.exitFullscreen();
      this.isFullscreen.set(false);
    }
  }

  onMouseMove(): void {
    this.showControls.set(true);
    if (this.hideControlsTimer) clearTimeout(this.hideControlsTimer);
    this.hideControlsTimer = setTimeout(() => {
      if (this.isPlaying()) this.showControls.set(false);
    }, 3000);
  }

  private fmtTime(s: number): string {
    const h = Math.floor(s / 3600);
    const m = Math.floor((s % 3600) / 60);
    const sec = Math.floor(s % 60);
    return h > 0
      ? `${h}:${m.toString().padStart(2, '0')}:${sec.toString().padStart(2, '0')}`
      : `${m}:${sec.toString().padStart(2, '0')}`;
  }

  ngOnDestroy(): void {
    this.hls?.destroy();
    if (this.hideControlsTimer) clearTimeout(this.hideControlsTimer);
  }
}
