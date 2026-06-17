import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Video } from '../models/video.model';
import { VideoDetail } from '../models/video-detail.model';
import { VideoStatus } from '../models/video-status.model';
import { MyVideo } from '../models/my-video.model';
import { UpdateVideoRequest, UpdateVideoResponse } from '../models/video-update.model';

@Injectable()
export class VideoService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/videos`;

  getAll(page = 1, pageSize = 20): Observable<Video[]> {
    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

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

  react(videoId: string, reactionTypeId: 1 | 2): Observable<{ likeCount: number; dislikeCount: number; userReaction: 1 | 2 | null }> {
    return this.http.post<{ likeCount: number; dislikeCount: number; userReaction: 1 | 2 | null }>(
      `${this.baseUrl}/${videoId}/reactions`,
      { reactionTypeId }
    );
  }
}
