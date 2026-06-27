import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HttpClient, HttpEventType } from '@angular/common/http';
import { FileUpload, FileUploadHandlerEvent, FileSelectEvent } from 'primeng/fileupload';
import { Button } from 'primeng/button';
import { Toast } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { TranslocoService, TranslocoPipe } from '@jsverse/transloco';
import { environment } from '../../../environments/environment';
import { VideoService, StorageInfo } from '../../core/services/video.service';

type UploadState = 'idle' | 'uploading' | 'success' | 'error';

@Component({
  selector: 'app-upload',
  imports: [FileUpload, Button, Toast, RouterLink, TranslocoPipe],
  providers: [MessageService, VideoService],
  templateUrl: './upload.html',
})
export class Upload implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly messageService = inject(MessageService);
  private readonly videoService = inject(VideoService);
  private readonly transloco = inject(TranslocoService);

  readonly state = signal<UploadState>('idle');
  readonly progress = signal(0);
  readonly selectedFile = signal<File | null>(null);
  readonly uploadedId = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);
  readonly storageInfo = signal<StorageInfo | null>(null);

  ngOnInit(): void {
    this.videoService.getStorageInfo().subscribe({
      next: info => this.storageInfo.set(info),
      error: () => {},
    });
  }

  onFileSelect(event: FileSelectEvent): void {
    const file = event.currentFiles[0] ?? null;
    this.selectedFile.set(file);
    this.state.set('idle');
    this.errorMessage.set(null);

    const info = this.storageInfo();
    if (file && info && file.size > info.freeBytes) {
      this.state.set('error');
      this.errorMessage.set(
        this.transloco.translate('upload.storageError', {
          required: this.formatSize(file.size),
          available: this.formatSize(info.freeBytes),
        })
      );
    }
  }

  onClear(): void {
    this.selectedFile.set(null);
    this.state.set('idle');
    this.progress.set(0);
    this.errorMessage.set(null);
  }

  onUpload(event: FileUploadHandlerEvent): void {
    const file = event.files[0];
    if (!file) return;

    const formData = new FormData();
    formData.append('file', file);

    this.state.set('uploading');
    this.progress.set(0);

    this.http.post<{ id: string; status: string }>(
      `${environment.apiUrl}/videos`,
      formData,
      { reportProgress: true, observe: 'events' }
    ).subscribe({
      next: e => {
        if (e.type === HttpEventType.UploadProgress && e.total) {
          this.progress.set(Math.round((e.loaded / e.total) * 100));
        } else if (e.type === HttpEventType.Response && e.body) {
          this.state.set('success');
          this.uploadedId.set(e.body.id);
          this.videoService.getStorageInfo().subscribe({
            next: info => this.storageInfo.set(info),
            error: () => {},
          });
        }
      },
      error: err => {
        this.state.set('error');
        const msg = err.error ?? 'Unknown error.';
        this.errorMessage.set(typeof msg === 'string' ? msg : JSON.stringify(msg));
        this.messageService.add({
          severity: 'error',
          summary: this.transloco.translate('upload.errorSummary'),
          detail: this.errorMessage() ?? '',
        });
      },
    });
  }

  retry(): void {
    this.state.set('idle');
    this.progress.set(0);
    this.errorMessage.set(null);
  }

  formatSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`;
  }
}
