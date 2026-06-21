import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Video } from '../models/video.model';
import { VideoDetail } from '../models/video-detail.model';
import { VideoStatus } from '../models/video-status.model';
import { MyVideo } from '../models/my-video.model';
import { UpdateVideoRequest, UpdateVideoResponse } from '../models/video-update.model';

export interface StorageInfo {
  usedBytes: number;
  totalBytes: number;
  freeBytes: number;
  usedPercent: number;
}

@Injectable()
export class VideoService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/videos`;

  getAll(page = 1, pageSize = 20, categoryId?: string | null, channelId?: string | null): Observable<Video[]> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    if (categoryId) params = params.set('categoryId', categoryId);
    if (channelId)  params = params.set('channelId',  channelId);

    return this.http.get<Video[]>(this.baseUrl, { params });
  }

  search(q: string, page = 1, pageSize = 20): Observable<Video[]> {
    const params = new HttpParams()
      .set('q', q)
      .set('page', page)
      .set('pageSize', pageSize);

    return this.http.get<Video[]>(this.baseUrl, { params });
  }

  getSuggestions(q: string): Observable<string[]> {
    const params = new HttpParams().set('q', q);
    return this.http.get<string[]>(`${this.baseUrl}/suggestions`, { params });
  }

  getById(id: string): Observable<VideoDetail> {
    return this.http.get<VideoDetail>(`${this.baseUrl}/${id}`);
  }

  getStatus(id: string): Observable<VideoStatus> {
    return this.http.get<VideoStatus>(`${this.baseUrl}/${id}/status`);
  }

  getMyVideos(): Observable<MyVideo[]> {
    return this.http.get<MyVideo[]>(`${this.baseUrl}/my`);
  }

  updateVideo(id: string, payload: UpdateVideoRequest): Observable<UpdateVideoResponse> {
    return this.http.put<UpdateVideoResponse>(`${this.baseUrl}/${id}`, payload);
  }

  getRelated(videoId: string, limit = 8): Observable<Video[]> {
    const params = new HttpParams().set('limit', limit);
    return this.http.get<Video[]>(`${this.baseUrl}/${videoId}/related`, { params });
  }

  react(videoId: string, reactionTypeId: 1 | 2): Observable<{ likeCount: number; dislikeCount: number; userReaction: 1 | 2 | null }> {
    return this.http.post<{ likeCount: number; dislikeCount: number; userReaction: 1 | 2 | null }>(
      `${this.baseUrl}/${videoId}/reactions`,
      { reactionTypeId }
    );
  }

  recordView(videoId: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${videoId}/views`, null, {
      headers: { 'X-Session-Id': this.getOrCreateSessionId() },
    });
  }

  getStorageInfo(): Observable<StorageInfo> {
    return this.http.get<StorageInfo>(`${environment.apiUrl}/users/me/storage`);
  }

  private getOrCreateSessionId(): string {
    let id = localStorage.getItem('clipo_session_id');
    if (!id) {
      id = typeof crypto?.randomUUID === 'function'
        ? crypto.randomUUID()
        : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, c => {
            const r = Math.random() * 16 | 0;
            return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
          });
      localStorage.setItem('clipo_session_id', id);
    }
    return id;
  }
}
