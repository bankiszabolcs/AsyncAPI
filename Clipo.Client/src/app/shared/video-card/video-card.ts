import { Component, computed, inject, input, OnDestroy, signal, viewChild, ElementRef, booleanAttribute } from '@angular/core';
import { RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoService, TranslocoPipe } from '@jsverse/transloco';
import { TimeAgoPipe } from '../pipes/time-ago.pipe';
import { ViewCountPipe } from '../pipes/view-count.pipe';
import { HttpClient } from '@angular/common/http';
import { DomSanitizer, SafeStyle } from '@angular/platform-browser';
import Hls from 'hls.js';
import { VideoThumbnail } from '../../core/models/video.model';
import { parseVtt, SpriteFrame } from '../video-player/vtt-parser';

export interface CardVideo {
  id: string;
  title: string;
  duration: number;
  publishedAt: string | null;
  viewCount?: number;
  statusId?: number;
  author?: { id: string; name: string; avatarUrl?: string | null };
  media: {
    thumbnails: VideoThumbnail[] | null;
    hoverStream?: string | null;
    preview?: string | null;
  };
}

@Component({
  selector: 'app-video-card',
  imports: [RouterLink, TimeAgoPipe, ViewCountPipe, TranslocoPipe],
  templateUrl: './video-card.html',
})
export class VideoCard implements OnDestroy {
  readonly video   = input.required<CardVideo>();
  readonly compact = input(false, { transform: booleanAttribute });

  private readonly http = inject(HttpClient);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly transloco = inject(TranslocoService);
  private readonly videoEl = viewChild<ElementRef<HTMLVideoElement>>('videoPreview');
  private hls: Hls | null = null;
  private readonly frames = signal<SpriteFrame[]>([]);
  private vttLoaded = false;

  private readonly activeLang = toSignal(this.transloco.langChanges$, {
    initialValue: this.transloco.getActiveLang(),
  });

  readonly isHovering = signal(false);
  readonly currentTime = signal(0);
  readonly thumbnailHoverRatio = signal<number | null>(null);

  readonly isCompleted = computed(() => {
    const s = this.video().statusId;
    return s === undefined || s === 3;
  });

  readonly srcset = computed(() =>
    (this.video().media.thumbnails ?? []).map(t => `${t.url} ${t.width}w`).join(', ')
  );

  readonly fallback = computed(() => {
    const thumbs = this.video().media.thumbnails;
    if (!thumbs?.length) return null;
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

  readonly spritePosition = computed(() => {
    const f = this.spriteFrame();
    return f ? `-${f.x}px -${f.y}px` : 'left top';
  });

  readonly tooltipLeft = computed(() => {
    const r = this.thumbnailHoverRatio() ?? 0;
    const half = (this.spriteFrame()?.w ?? 160) / 2;
    return `clamp(${half}px, ${r * 100}%, calc(100% - ${half}px))`;
  });

  readonly hoverTime = computed(() => {
    const r = this.thumbnailHoverRatio();
    return r !== null ? this.formatTime(Math.round(r * this.video().duration)) : '';
  });

  readonly statusLabelText = computed(() => {
    void this.activeLang();
    const keys: Record<number, string> = {
      1: 'videoCard.status.queued',
      2: 'videoCard.status.processing',
      4: 'videoCard.status.failed',
    };
    const key = keys[this.video().statusId ?? 3];
    return key ? this.transloco.translate(key) : '';
  });

  readonly statusBadgeClass = computed(() => {
    const classes: Record<number, string> = {
      1: 'bg-gray-500/20 text-gray-400',
      2: 'bg-blue-500/20 text-blue-400',
      4: 'bg-red-500/20 text-red-400',
    };
    return classes[this.video().statusId ?? 3] ?? '';
  });

  onMouseEnter(): void {
    if (!this.isCompleted()) return;
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
    if (!url) return;
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
