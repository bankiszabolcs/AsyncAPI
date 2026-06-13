import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Navbar } from './layout/navbar/navbar';
import { Sidebar } from './layout/sidebar/sidebar';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Navbar, Sidebar],
  templateUrl: './app.html',
})
export class App {
  // Fekvő tableten (~1280px alatt) vagy kisebb kijelzőn alapból csukva töltsön be a menü.
  readonly sidebarCollapsed = signal(this.isSmallScreen());

  toggleSidebar(): void {
    this.sidebarCollapsed.update(v => !v);
  }

  private isSmallScreen(): boolean {
    return typeof window !== 'undefined' && window.innerWidth < 1280;
  }
}
