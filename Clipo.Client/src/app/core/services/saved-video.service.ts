import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SavedVideoItem } from '../models/saved-video.model';

@Injectable()
export class SavedVideoService {
  private readonly http    = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/saved-videos`;

  getSavedVideos(): Observable<SavedVideoItem[]> {
    return this.http.get<SavedVideoItem[]>(this.baseUrl);
  }

  getStatus(videoId: string): Observable<{ isSaved: boolean }> {
    return this.http.get<{ isSaved: boolean }>(`${this.baseUrl}/${videoId}/status`);
  }

  save(videoId: string): Observable<{ isSaved: boolean }> {
    return this.http.post<{ isSaved: boolean }>(`${this.baseUrl}/${videoId}`, {});
  }

  unsave(videoId: string): Observable<{ isSaved: boolean }> {
    return this.http.delete<{ isSaved: boolean }>(`${this.baseUrl}/${videoId}`);
  }
}
