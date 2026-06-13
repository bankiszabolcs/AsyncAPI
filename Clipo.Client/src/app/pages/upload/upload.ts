import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HttpClient, HttpEventType } from '@angular/common/http';
import { FileUpload, FileUploadHandlerEvent, FileSelectEvent } from 'primeng/fileupload';
import { Button } from 'primeng/button';
import { ProgressBar } from 'primeng/progressbar';
import { Toast } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { environment } from '../../../environments/environment';

type UploadState = 'idle' | 'uploading' | 'success' | 'error';

@Component({
  selector: 'app-upload',
  imports: [FileUpload, Button, ProgressBar, Toast, RouterLink],
  providers: [MessageService],
  templateUrl: './upload.html',
})
export class Upload {
  private readonly http = inject(HttpClient);
  private readonly messageService = inject(MessageService);

  readonly state = signal<UploadState>('idle');
  readonly progress = signal(0);
  readonly selectedFile = signal<File | null>(null);
  readonly uploadedId = signal<string | null>(null);
  readonly errorMessage = signal<string | null>(null);

  onFileSelect(event: FileSelectEvent): void {
    this.selectedFile.set(event.currentFiles[0] ?? null);
    this.state.set('idle');
    this.errorMessage.set(null);
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
        }
      },
      error: err => {
        this.state.set('error');
        const msg = err.error ?? 'Ismeretlen hiba történt.';
        this.errorMessage.set(typeof msg === 'string' ? msg : JSON.stringify(msg));
        this.messageService.add({ severity: 'error', summary: 'Hiba', detail: this.errorMessage() ?? '' });
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
