import { Component, inject, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, of, switchMap } from 'rxjs';
import { Button } from 'primeng/button';
import { Dialog } from 'primeng/dialog';
import { Toast } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { PlaylistService } from '../../core/services/playlist.service';
import { VideoService } from '../../core/services/video.service';
import { PlaylistDetail as PlaylistDetailData, PlaylistVideoItem } from '../../core/models/playlist.model';
import { MyVideo } from '../../core/models/my-video.model';
import { PlaylistFormDialog, PlaylistFormData } from '../../shared/playlist-form-dialog/playlist-form-dialog';

@Component({
  selector: 'app-playlist-detail',
  imports: [RouterLink, Button, Dialog, Toast, PlaylistFormDialog],
  providers: [PlaylistService, VideoService, MessageService],
  templateUrl: './playlist-detail.html',
})
export class PlaylistDetail implements OnInit {
  private readonly route          = inject(ActivatedRoute);
  private readonly router         = inject(Router);
  private readonly playlistService = inject(PlaylistService);
  private readonly videoService   = inject(VideoService);
  private readonly messageService = inject(MessageService);

  readonly playlist   = signal<PlaylistDetailData | null>(null);
  readonly isLoading  = signal(true);
  readonly myVideos   = signal<MyVideo[]>([]);

  readonly showEditDialog     = signal(false);
  readonly showAddDialog      = signal(false);
  readonly showDeleteDialog   = signal(false);
  readonly isSaving           = signal(false);
  readonly addingVideoId      = signal<string | null>(null);

  readonly editTitle = signal('');
  readonly editDesc  = signal('');
  readonly editVis   = signal(3);

  readonly addedVideoIds = computed(() =>
    new Set((this.playlist()?.items ?? []).map(i => i.videoId))
  );

  readonly availableVideos = computed(() =>
    this.myVideos().filter(v => !this.addedVideoIds().has(v.id))
  );

  private playlistId = '';

  ngOnInit(): void {
    this.route.params.pipe(
      switchMap(params => {
        this.playlistId = params['id'];
        return this.playlistService.getPlaylist(this.playlistId).pipe(catchError(() => of(null)));
      })
    ).subscribe(p => {
      this.playlist.set(p);
      this.isLoading.set(false);
    });
  }

  openEdit(): void {
    const p = this.playlist();
    if (!p) return;
    this.editTitle.set(p.title);
    this.editDesc.set(p.description ?? '');
    this.editVis.set(p.visibilityId);
    this.showEditDialog.set(true);
  }

  saveEdit(data: PlaylistFormData): void {
    this.isSaving.set(true);
    this.playlistService.update(this.playlistId, {
      title: data.title,
      description: data.description,
      visibilityId: data.visibilityId,
    }).subscribe({
      next: updated => {
        this.playlist.update(p => p ? { ...p, title: updated.title, description: updated.description, visibilityId: updated.visibilityId } : p);
        this.showEditDialog.set(false);
        this.isSaving.set(false);
        this.messageService.add({ severity: 'success', summary: 'Mentve', life: 3000 });
      },
      error: () => {
        this.isSaving.set(false);
        this.messageService.add({ severity: 'error', summary: 'Hiba', detail: 'Nem sikerült menteni.', life: 4000 });
      },
    });
  }

  openAddVideo(): void {
    this.videoService.getMyVideos().pipe(catchError(() => of([] as MyVideo[])))
      .subscribe(videos => {
        this.myVideos.set(videos.filter(v => v.statusId === 3));
        this.showAddDialog.set(true);
      });
  }

  addVideo(videoId: string): void {
    this.addingVideoId.set(videoId);
    this.playlistService.addVideo(this.playlistId, videoId).subscribe({
      next: item => {
        this.addingVideoId.set(null);
        const video = this.myVideos().find(v => v.id === videoId);
        if (video) {
          this.playlist.update(p => p ? {
            ...p,
            items: [...p.items, {
              videoId: item.videoId,
              position: item.position,
              video: {
                id: video.id,
                title: video.title,
                duration: video.duration ?? 0,
                thumbnails: video.media.thumbnails ?? [],
              }
            }]
          } : p);
        }
        this.messageService.add({ severity: 'success', summary: 'Hozzáadva', life: 3000 });
      },
      error: () => {
        this.addingVideoId.set(null);
        this.messageService.add({ severity: 'error', summary: 'Hiba', detail: 'Nem sikerült hozzáadni.', life: 4000 });
      },
    });
  }

  removeVideo(videoId: string): void {
    this.playlistService.removeVideo(this.playlistId, videoId).subscribe({
      next: () => {
        this.playlist.update(p => p ? {
          ...p,
          items: p.items
            .filter(i => i.videoId !== videoId)
            .map((i, idx) => ({ ...i, position: idx + 1 }))
        } : p);
        this.messageService.add({ severity: 'success', summary: 'Eltávolítva', life: 3000 });
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Hiba', detail: 'Nem sikerült eltávolítani.', life: 4000 });
      },
    });
  }

  moveUp(item: PlaylistVideoItem): void {
    const items = [...(this.playlist()?.items ?? [])];
    const idx = items.findIndex(i => i.videoId === item.videoId);
    if (idx <= 0) return;

    const newPos = items[idx - 1].position;
    const oldPos = item.position;

    items[idx - 1] = { ...items[idx - 1], position: oldPos };
    items[idx]     = { ...item, position: newPos };

    this.playlist.update(p => p ? { ...p, items } : p);

    this.playlistService.updateVideoPosition(this.playlistId, item.videoId, newPos).subscribe({
      error: () => this.messageService.add({ severity: 'error', summary: 'Hiba', detail: 'Pozíció nem frissíthető.', life: 3000 }),
    });
    this.playlistService.updateVideoPosition(this.playlistId, items[idx - 1].videoId, oldPos).subscribe();
  }

  moveDown(item: PlaylistVideoItem): void {
    const items = [...(this.playlist()?.items ?? [])];
    const idx = items.findIndex(i => i.videoId === item.videoId);
    if (idx < 0 || idx >= items.length - 1) return;

    const newPos = items[idx + 1].position;
    const oldPos = item.position;

    items[idx + 1] = { ...items[idx + 1], position: oldPos };
    items[idx]     = { ...item, position: newPos };

    this.playlist.update(p => p ? { ...p, items } : p);

    this.playlistService.updateVideoPosition(this.playlistId, item.videoId, newPos).subscribe({
      error: () => this.messageService.add({ severity: 'error', summary: 'Hiba', detail: 'Pozíció nem frissíthető.', life: 3000 }),
    });
    this.playlistService.updateVideoPosition(this.playlistId, items[idx + 1].videoId, oldPos).subscribe();
  }

  confirmDeletePlaylist(): void {
    this.showDeleteDialog.set(true);
  }

  deletePlaylist(): void {
    this.playlistService.delete(this.playlistId).subscribe({
      next: () => {
        this.showDeleteDialog.set(false);
        this.router.navigate(['/channel']);
      },
      error: () => {
        this.messageService.add({ severity: 'error', summary: 'Hiba', detail: 'Nem sikerült törölni.', life: 4000 });
      },
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
