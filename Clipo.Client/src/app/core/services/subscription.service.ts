import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Channel } from '../models/channel.model';

export interface SubscribeResponse {
  subscriberCount: number;
  isSubscribed: boolean;
}

@Injectable({ providedIn: 'root' })
export class SubscriptionService {
  private readonly http    = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/users`;

  getChannel(id: string): Observable<Channel> {
    return this.http.get<Channel>(`${this.baseUrl}/${id}`);
  }

  getMySubscriptions(): Observable<Channel[]> {
    return this.http.get<Channel[]>(`${this.baseUrl}/me/subscriptions`);
  }

  subscribe(channelId: string): Observable<SubscribeResponse> {
    return this.http.post<SubscribeResponse>(`${this.baseUrl}/${channelId}/subscribe`, {});
  }

  unsubscribe(channelId: string): Observable<SubscribeResponse> {
    return this.http.delete<SubscribeResponse>(`${this.baseUrl}/${channelId}/subscribe`);
  }
}
