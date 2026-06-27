import { Component, signal, HostListener, inject } from '@angular/core';
import { RouterOutlet, RouterLink } from '@angular/router';
import { Drawer } from 'primeng/drawer';
import { Toast } from 'primeng/toast';
import { ToastMessageOptions } from 'primeng/api';
import { Navbar } from './layout/navbar/navbar';
import { Sidebar } from './layout/sidebar/sidebar';
import { LoadingService } from './core/services/loading.service';
import { ProgressBar } from 'primeng/progressbar';
import { NotificationService } from './core/services/notification.service';
import { AppNotification } from './core/models/notification.model';
import { ThemeService } from './core/services/theme.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, Drawer, Toast, Navbar, Sidebar, ProgressBar],
  templateUrl: './app.html',
})
export class App {
  readonly loading   = inject(LoadingService);
  private readonly notifSvc = inject(NotificationService);
  // ThemeService injection triggers its constructor effect which applies the saved theme
  private readonly _theme = inject(ThemeService);

  onToastClick(msg: ToastMessageOptions, closeFn: (e: Event) => void, event: Event): void {
    const n = msg.data as AppNotification | undefined;
    if (!n?.link) return;
    closeFn(event);
    this.notifSvc.onNotificationRead(n.id);
    this.notifSvc.navigateTo(n.link);
  }
  // Mobil/tablet nézet (~1280px alatt): nincs beágyazott menüsor, csak a
  // hamburgerrel előhúzható drawer. Asztali nézetben a menü helyben marad,
  // a hamburger csak összecsukja/kinyitja.
  readonly isMobile = signal(this.checkMobile());
  readonly sidebarCollapsed = signal(this.checkMobile());
  readonly mobileSidebarOpen = signal(false);

  toggleSidebar(): void {
    if (this.isMobile()) {
      this.mobileSidebarOpen.update(v => !v);
    } else {
      this.sidebarCollapsed.update(v => !v);
    }
  }

  @HostListener('window:resize')
  onResize(): void {
    const mobile = this.checkMobile();
    this.isMobile.set(mobile);
    // Asztali nézetre váltva a mobil drawer ne maradjon nyitva.
    if (!mobile) {
      this.mobileSidebarOpen.set(false);
    }
  }

  private checkMobile(): boolean {
    return typeof window !== 'undefined' && window.innerWidth < 1280;
  }
}
