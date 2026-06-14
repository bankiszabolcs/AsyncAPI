import { Component, signal, HostListener } from '@angular/core';
import { RouterOutlet, RouterLink } from '@angular/router';
import { Drawer } from 'primeng/drawer';
import { Navbar } from './layout/navbar/navbar';
import { Sidebar } from './layout/sidebar/sidebar';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, Drawer, Navbar, Sidebar],
  templateUrl: './app.html',
})
export class App {
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
