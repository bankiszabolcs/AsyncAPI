import { Component, computed, DestroyRef, ElementRef, inject, OnInit, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { interval, switchMap, takeWhile } from 'rxjs';
import { ImageCropperComponent, ImageTransform } from 'ngx-image-cropper';
import { Button } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Dialog } from 'primeng/dialog';
import { Slider } from 'primeng/slider';
import { AuthService, UserProfile } from '../../../core/auth/auth.service';

type AvatarState = 'idle' | 'uploading' | 'processing' | 'done' | 'error';
type SaveState   = 'idle' | 'saving'    | 'saved'      | 'error';

@Component({
  selector: 'app-profile',
  imports: [FormsModule, ImageCropperComponent, Button, InputText, Dialog, Slider],
  templateUrl: './profile.html',
})
export class Profile implements OnInit {
  private readonly http       = inject(HttpClient);
  private readonly destroyRef = inject(DestroyRef);
  readonly auth               = inject(AuthService);

  private readonly fileInput  = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');
  private readonly cropperRef = viewChild(ImageCropperComponent);

  readonly displayName      = signal('');
  readonly saveState        = signal<SaveState>('idle');
  readonly avatarState      = signal<AvatarState>('idle');
  readonly cropperVisible   = signal(false);
  readonly cropperFile      = signal<File | undefined>(undefined);
  readonly avatarPreviewUrl = signal<string | null>(null);

  readonly zoom      = signal(0);
  readonly transform = computed<ImageTransform>(() => {
    const z = this.zoom();
    return { scale: z >= 0 ? z + 1 : Math.max(0.1, 1 + z) };
  });

  ngOnInit(): void {
    const p = this.auth.profile();
    if (p) this.displayName.set(p.displayName ?? p.username);
  }

  openFilePicker(): void {
    this.fileInput().nativeElement.click();
  }

  onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;
    this.zoom.set(0);
    this.cropperFile.set(file);
    this.cropperVisible.set(true);
  }

  onImageCropped(objectUrl: string | null | undefined): void {
    if (!objectUrl) return;
    this.revokePreview();
    this.avatarPreviewUrl.set(objectUrl);
  }

  cancelCrop(): void {
    this.cropperVisible.set(false);
    this.cropperFile.set(undefined);
    this.zoom.set(0);
    this.fileInput().nativeElement.value = '';
  }

  confirmCrop(): void {
    const result = this.cropperRef()?.crop('blob');
    if (!result) return;
    result.then(event => {
      if (!event?.blob) return;
      this.cropperVisible.set(false);
      this.zoom.set(0);
      this.fileInput().nativeElement.value = '';
      this.uploadAvatar(event.blob);
    });
  }

  private uploadAvatar(blob: Blob): void {
    const formData = new FormData();
    formData.append('file', blob, 'avatar.jpg');

    this.avatarState.set('uploading');
    this.http.post<{ id: string }>('/api/thumbnails', formData).subscribe({
      next: ({ id }) => { this.avatarState.set('processing'); this.pollAvatarStatus(id); },
      error: ()       => this.avatarState.set('error'),
    });
  }

  private pollAvatarStatus(id: string): void {
    interval(2000).pipe(
      switchMap(() => this.http.get<{ status: string }>(`/api/thumbnails/${id}/status`)),
      takeWhile(s => s.status !== 'Completed' && s.status !== 'Failed', true),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe(s => {
      if (s.status === 'Completed') {
        this.http.patch<UserProfile>('/api/users/me', { avatarImageId: id }).subscribe({
          next: user => {
            this.auth.profile.set(user);
            this.revokePreview();
            this.avatarState.set('done');
            setTimeout(() => this.avatarState.set('idle'), 2500);
          },
          error: () => this.avatarState.set('error'),
        });
      } else if (s.status === 'Failed') {
        this.avatarState.set('error');
      }
    });
  }

  saveDisplayName(): void {
    const name = this.displayName().trim();
    if (!name) return;
    this.saveState.set('saving');
    this.http.patch<UserProfile>('/api/users/me', { displayName: name }).subscribe({
      next: user => {
        this.auth.profile.set(user);
        this.saveState.set('saved');
        setTimeout(() => this.saveState.set('idle'), 2500);
      },
      error: () => this.saveState.set('error'),
    });
  }

  private revokePreview(): void {
    const prev = this.avatarPreviewUrl();
    if (prev) URL.revokeObjectURL(prev);
    this.avatarPreviewUrl.set(null);
  }
}
