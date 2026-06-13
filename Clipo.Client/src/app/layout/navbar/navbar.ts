import { Component, output, signal, HostListener } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Button } from 'primeng/button';
import { Avatar } from 'primeng/avatar';
import { Menu } from 'primeng/menu';
import { Drawer } from 'primeng/drawer';
import { Popover } from 'primeng/popover';
import { Ripple } from 'primeng/ripple';
import { MenuItem } from 'primeng/api';

@Component({
  selector: 'app-navbar',
  imports: [RouterLink, Button, Avatar, Menu, Drawer, Popover, Ripple],
  templateUrl: './navbar.html',
})
export class Navbar {
  readonly sidebarToggle = output<void>();

  // Asztali nézet alatt (~1280px) a profil drawerként nyílik, fölötte legördülő menüként.
  readonly isMobile = signal(this.checkMobile());
  readonly drawerVisible = signal(false);

  readonly userMenuItems: MenuItem[] = [
    { label: 'A csatornám', icon: 'pi pi-user',     routerLink: '/channel' },
    { label: 'Stúdió',      icon: 'pi pi-video',    routerLink: '/studio' },
    { label: 'Beállítások', icon: 'pi pi-cog',      routerLink: '/settings' },
    { separator: true },
    { label: 'Kijelentkezés', icon: 'pi pi-sign-out', command: () => this.logout() },
  ];

  @HostListener('window:resize')
  onResize(): void {
    const mobile = this.checkMobile();
    this.isMobile.set(mobile);
    // Asztali nézetre váltva ne maradjon nyitva a drawer.
    if (!mobile) {
      this.drawerVisible.set(false);
    }
  }

  onAvatarClick(menu: Menu, event: Event): void {
    if (this.isMobile()) {
      this.drawerVisible.set(true);
    } else {
      menu.toggle(event);
    }
  }

  onDrawerItemClick(item: MenuItem): void {
    item.command?.({ originalEvent: undefined as never, item });
    this.drawerVisible.set(false);
  }

  private logout(): void {
    // TODO: kijelentkezés logika
  }

  private checkMobile(): boolean {
    return typeof window !== 'undefined' && window.innerWidth < 1280;
  }
}
