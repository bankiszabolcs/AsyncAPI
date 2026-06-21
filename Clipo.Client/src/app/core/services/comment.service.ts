import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Comment } from '../models/comment.model';

@Injectable({ providedIn: 'root' })
export class CommentService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  getByVideoId(videoId: string): Observable<Comment[]> {
    return this.http.get<Comment[]>(`${this.base}/videos/${videoId}/comments`);
  }

  create(videoId: string, content: string, parentCommentId?: string): Observable<Comment> {
    return this.http.post<Comment>(`${this.base}/videos/${videoId}/comments`, {
      content,
      parentCommentId: parentCommentId ?? null,
    });
  }

  update(id: string, content: string): Observable<Comment> {
    return this.http.put<Comment>(`${this.base}/comments/${id}`, { content });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/comments/${id}`);
  }

  openStream(videoId: string): EventSource {
    return new EventSource(`${this.base}/videos/${videoId}/comments/stream`);
  }
}
