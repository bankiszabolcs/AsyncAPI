import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { WatchLaterItem } from '../models/watch-later.model';

@Injectable()
export class WatchLaterService {
  private readonly http    = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/watch-later`;

  getWatchLater(): Observable<WatchLaterItem[]> {
    return this.http.get<WatchLaterItem[]>(this.baseUrl);
  }

  getStatus(videoId: string): Observable<{ isAdded: boolean }> {
    return this.http.get<{ isAdded: boolean }>(`${this.baseUrl}/${videoId}/status`);
  }

  add(videoId: string): Observable<{ isAdded: boolean }> {
    return this.http.post<{ isAdded: boolean }>(`${this.baseUrl}/${videoId}`, {});
  }

  remove(videoId: string): Observable<{ isAdded: boolean }> {
    return this.http.delete<{ isAdded: boolean }>(`${this.baseUrl}/${videoId}`);
  }
}
