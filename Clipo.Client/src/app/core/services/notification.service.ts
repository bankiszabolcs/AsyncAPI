import { effect, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { environment } from '../../../environments/environment';
import { AppNotification } from '../models/notification.model';
import { AuthService } from '../auth/auth.service';

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly http           = inject(HttpClient);
  private readonly auth           = inject(AuthService);
  private readonly router         = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly baseUrl        = `${environment.apiUrl}/notifications`;

  readonly unreadCount   = signal(0);
  readonly notifications = signal<AppNotification[] | undefined>(undefined);
  readonly isLoading     = signal(false);

  private pollHandle?: ReturnType<typeof setInterval>;
  private prevCount   = -1;
  private knownIds    = new Set<string>();

  constructor() {
    effect(() => {
      if (this.auth.isLoggedIn()) {
        this.refreshCount();
        this.pollHandle ??= setInterval(() => this.refreshCount(), 60_000);
      } else {
        this.unreadCount.set(0);
        this.notifications.set(undefined);
        this.prevCount = -1;
        this.knownIds.clear();
        if (this.pollHandle) { clearInterval(this.pollHandle); this.pollHandle = undefined; }
      }
    });
  }

  loadNotifications(): void {
    if (this.isLoading()) return;
    this.isLoading.set(true);
    this.http.get<AppNotification[]>(this.baseUrl).subscribe({
      next: items => {
        items.forEach(n => this.knownIds.add(n.id));
        this.notifications.set(items);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }

  markRead(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/read`, {});
  }

  markAllRead(): void {
    this.http.post<void>(`${this.baseUrl}/read-all`, {}).subscribe(() => {
      this.notifications.update(items => items?.map(n => ({ ...n, isRead: true })));
      this.unreadCount.set(0);
    });
  }

  onNotificationRead(id: string): void {
    this.markRead(id).subscribe();
    this.notifications.update(items =>
      items?.map(n => n.id === id ? { ...n, isRead: true } : n)
    );
    this.unreadCount.update(c => Math.max(0, c - 1));
  }

  navigateTo(link: string): void {
    const url = new URL(link, 'http://x');
    const queryParams: Record<string, string> = {};
    url.searchParams.forEach((v, k) => { queryParams[k] = v; });
    this.router.navigate([url.pathname], {
      queryParams: Object.keys(queryParams).length ? queryParams : undefined,
    });
  }

  private refreshCount(): void {
    this.http.get<{ count: number }>(`${this.baseUrl}/unread-count`).subscribe({
      next: r => {
        const newCount = r.count;
        if (this.prevCount === -1) {
          this.initKnownNotifs();
        } else if (newCount > this.prevCount) {
          this.fetchAndToastNew();
        }
        this.prevCount = newCount;
        this.unreadCount.set(newCount);
      },
      error: () => {},
    });
  }

  private initKnownNotifs(): void {
    this.http.get<AppNotification[]>(this.baseUrl).subscribe({
      next: items => {
        items.forEach(n => this.knownIds.add(n.id));
        this.notifications.set(items);
      },
      error: () => {},
    });
  }

  private fetchAndToastNew(): void {
    this.http.get<AppNotification[]>(this.baseUrl).subscribe({
      next: items => {
        this.notifications.set(items);
        items
          .filter(n => !n.isRead && !this.knownIds.has(n.id))
          .forEach(n => {
            this.knownIds.add(n.id);
            this.messageService.add({
              severity: 'info',
              summary: n.title,
              detail: n.body ?? undefined,
              life: 6000,
              data: n,
            });
          });
      },
      error: () => {},
    });
  }
}
