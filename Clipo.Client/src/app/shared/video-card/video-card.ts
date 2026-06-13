import { Component, computed, inject, input, OnDestroy, signal, viewChild, ElementRef } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TimeAgoPipe } from '../pipes/time-ago.pipe';
import { HttpClient } from '@angular/common/http';
import { DomSanitizer, SafeStyle } from '@angular/platform-browser';
import Hls from 'hls.js';
import { Video } from '../../core/models/video.model';
import { parseVtt, SpriteFrame } from '../video-player/vtt-parser';

@Component({
  selector: 'app-video-card',
  imports: [RouterLink, TimeAgoPipe],
  templateUrl: './video-card.html',
})
export class VideoCard implements OnDestroy {
  readonly video = input.required<Video>();

  private readonly http = inject(HttpClient);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly videoEl = viewChild<ElementRef<HTMLVideoElement>>('videoPreview');
  private hls: Hls | null = null;
  private readonly frames = signal<SpriteFrame[]>([]);
  private vttLoaded = false;

  readonly isHovering = signal(false);
  readonly currentTime = signal(0);
  readonly thumbnailHoverRatio = signal<number | null>(null);

  readonly srcset = computed(() =>
    this.video().media.thumbnails.map(t => `${t.url} ${t.width}w`).join(', ')
  );

  readonly fallback = computed(() => {
    const thumbs = this.video().media.thumbnails;
    if (!thumbs.length) return null;
    return thumbs.reduce((best, t) => (t.width > best.width ? t : best)).url;
  });

  readonly progress = computed(() =>
    (this.currentTime() / this.video().duration) * 100
  );

  readonly displayDuration = computed(() => {
    const secs = this.isHovering()
      ? Math.max(0, this.video().duration - this.currentTime())
      : this.video().duration;
    return this.formatTime(secs);
  });

  readonly spriteFrame = computed((): SpriteFrame | null => {
    const r = this.thumbnailHoverRatio();
    const frames = this.frames();
    if (r === null || !frames.length) return null;
    const t = r * this.video().duration;
    return frames.find(f => t >= f.start && t < f.end) ?? frames[frames.length - 1];
  });

  readonly safeSpriteStyle = computed((): SafeStyle | null => {
    const f = this.spriteFrame();
    if (!f) return null;
    return this.sanitizer.bypassSecurityTrustStyle(`url("${f.url}")`);
  });

  readonly hoverTime = computed(() => {
    const r = this.thumbnailHoverRatio();
    return r !== null ? this.formatTime(Math.round(r * this.video().duration)) : '';
  });

  onMouseEnter(): void {
    this.isHovering.set(true);
    this.currentTime.set(0);
    this.loadVttOnce();
    this.startPreview();
  }

  onMouseLeave(): void {
    this.isHovering.set(false);
    this.thumbnailHoverRatio.set(null);
    this.stopPreview();
  }

  onThumbnailMove(e: MouseEvent): void {
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    const r = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
    this.thumbnailHoverRatio.set(r);
  }

  onSeekBarLeave(): void {
    this.thumbnailHoverRatio.set(null);
  }

  seekPreview(e: MouseEvent): void {
    e.stopPropagation();
    e.preventDefault();
    const rect = (e.currentTarget as HTMLElement).getBoundingClientRect();
    const r = Math.max(0, Math.min(1, (e.clientX - rect.left) / rect.width));
    const el = this.videoEl()?.nativeElement;
    if (el && el.duration) {
      el.currentTime = r * el.duration;
      this.currentTime.set(Math.floor(el.currentTime));
    }
  }

  onTimeUpdate(event: Event): void {
    this.currentTime.set(Math.floor((event.target as HTMLVideoElement).currentTime));
  }

  private loadVttOnce(): void {
    if (this.vttLoaded) return;
    this.vttLoaded = true;
    const url = this.video().media.preview;
    if (!url) return;
    this.http.get(url, { responseType: 'text' }).subscribe({
      next: text => { this.frames.set(parseVtt(text, url)); },
    });
  }

  private startPreview(): void {
    const el = this.videoEl()?.nativeElement;
    if (!el) return;
    const url = this.video().media.hoverStream;
    if (Hls.isSupported()) {
      this.hls = new Hls();
      this.hls.loadSource(url);
      this.hls.attachMedia(el);
      this.hls.on(Hls.Events.MANIFEST_PARSED, () => el.play().catch(() => {}));
    } else if (el.canPlayType('application/vnd.apple.mpegurl')) {
      el.src = url;
      el.play().catch(() => {});
    }
  }

  private stopPreview(): void {
    const el = this.videoEl()?.nativeElement;
    if (el) { el.pause(); el.removeAttribute('src'); el.load(); }
    this.hls?.destroy();
    this.hls = null;
    this.currentTime.set(0);
  }

  private formatTime(seconds: number): string {
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    const s = seconds % 60;
    return h > 0
      ? `${h}:${m.toString().padStart(2, '0')}:${s.toString().padStart(2, '0')}`
      : `${m}:${s.toString().padStart(2, '0')}`;
  }

  ngOnDestroy(): void {
    this.stopPreview();
  }
}
