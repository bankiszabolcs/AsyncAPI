import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreatePlaylistRequest,
  Playlist,
  PlaylistDetail,
  UpdatePlaylistRequest,
} from '../models/playlist.model';

@Injectable()
export class PlaylistService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/playlists`;

  getMyPlaylists(): Observable<Playlist[]> {
    return this.http.get<Playlist[]>(this.baseUrl);
  }

  getPlaylist(id: string): Observable<PlaylistDetail> {
    return this.http.get<PlaylistDetail>(`${this.baseUrl}/${id}`);
  }

  create(request: CreatePlaylistRequest): Observable<Playlist> {
    return this.http.post<Playlist>(this.baseUrl, request);
  }

  update(id: string, request: UpdatePlaylistRequest): Observable<Playlist> {
    return this.http.put<Playlist>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  addVideo(playlistId: string, videoId: string): Observable<{ videoId: string; position: number }> {
    return this.http.post<{ videoId: string; position: number }>(
      `${this.baseUrl}/${playlistId}/videos`,
      { videoId }
    );
  }

  removeVideo(playlistId: string, videoId: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${playlistId}/videos/${videoId}`);
  }

  updateVideoPosition(playlistId: string, videoId: string, position: number): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${playlistId}/videos/${videoId}`, { position });
  }
}
