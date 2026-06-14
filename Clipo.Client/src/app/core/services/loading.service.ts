import { Injectable, signal, computed } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class LoadingService {
  private activeRequests = signal(0);

  readonly isLoading = computed(() => this.activeRequests() > 0);

  increment(): void {
    this.activeRequests.update(n => n + 1);
  }

  decrement(): void {
    this.activeRequests.update(n => Math.max(0, n - 1));
  }
}
