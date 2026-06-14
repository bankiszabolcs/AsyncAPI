import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { OAuthService } from 'angular-oauth2-oidc';
import { firstValueFrom } from 'rxjs';
import { authConfig } from './auth.config';

export interface UserProfile {
  id: string;
  username: string;
  email: string;
  displayName: string | null;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly oauth = inject(OAuthService);
  private readonly http = inject(HttpClient);

  readonly isLoggedIn = signal(false);
  readonly profile = signal<UserProfile | null>(null);

  readonly user = computed(() => {
    const p = this.profile();
    if (p) return { name: p.displayName ?? p.username, email: p.email, picture: null };

    // Fallback: ha az API hívás még nem tért vissza, a JWT claimekből vesszük
    if (!this.isLoggedIn()) return null;
    const claims = this.oauth.getIdentityClaims() as Record<string, string> | null;
    if (!claims) return null;
    return {
      name: claims['name'] ?? claims['preferred_username'] ?? '',
      email: claims['email'] ?? '',
      picture: claims['picture'] ?? null,
    };
  });

  async init(): Promise<void> {
    this.oauth.configure(authConfig);
    this.oauth.setupAutomaticSilentRefresh();
    await this.oauth.loadDiscoveryDocumentAndTryLogin();

    const loggedIn = this.oauth.hasValidAccessToken();
    this.isLoggedIn.set(loggedIn);

    if (loggedIn) {
      await this.fetchProfile();
    }
  }

  login(): void {
    this.oauth.initCodeFlow();
  }

  logout(): void {
    this.oauth.logOut();
    this.isLoggedIn.set(false);
    this.profile.set(null);
  }

  getAccessToken(): string {
    return this.oauth.getAccessToken();
  }

  private async fetchProfile(): Promise<void> {
    try {
      const p = await firstValueFrom(this.http.get<UserProfile>('/api/users/me'));
      this.profile.set(p);
    } catch {
      // Nem blokkolja az app indulást ha az API nem elérhető
    }
  }
}
