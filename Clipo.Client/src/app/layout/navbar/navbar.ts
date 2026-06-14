import { Component, output, signal, inject, HostListener } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { Button } from 'primeng/button';
import { Avatar } from 'primeng/avatar';
import { Menu } from 'primeng/menu';
import { Drawer } from 'primeng/drawer';
import { Popover } from 'primeng/popover';
import { Ripple } from 'primeng/ripple';
import { MenuItem } from 'primeng/api';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-navbar',
  imports: [RouterLink, NgTemplateOutlet, Button, Avatar, Menu, Drawer, Popover, Ripple],
  templateUrl: './navbar.html',
})
export class Navbar {
  readonly sidebarToggle = output<void>();
  readonly auth = inject(AuthService);

  // Asztali nézet alatt (~1280px) a profil/értesítés/keresés drawerként nyílik,
  // fölötte legördülő menüként / popoverként / beágyazott keresőmezőként.
  readonly isMobile = signal(this.checkMobile());
  readonly drawerVisible = signal(false);
  readonly searchDrawerVisible = signal(false);
  readonly notifDrawerVisible = signal(false);

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
    // Asztali nézetre váltva ne maradjanak nyitva a mobil drawerek.
    if (!mobile) {
      this.drawerVisible.set(false);
      this.searchDrawerVisible.set(false);
      this.notifDrawerVisible.set(false);
    }
  }

  onAvatarClick(menu: Menu, event: Event): void {
    if (this.isMobile()) {
      this.drawerVisible.set(true);
    } else {
      menu.toggle(event);
    }
  }

  // Értesítések: mobilon jobbról beúszó drawer, asztalin popover.
  onBellClick(popover: Popover, event: Event): void {
    if (this.isMobile()) {
      this.notifDrawerVisible.set(true);
    } else {
      popover.toggle(event);
    }
  }

  onDrawerItemClick(item: MenuItem): void {
    item.command?.({ originalEvent: undefined as never, item });
    this.drawerVisible.set(false);
  }

  private logout(): void {
    this.auth.logout();
  }

  private checkMobile(): boolean {
    return typeof window !== 'undefined' && window.innerWidth < 1280;
  }
}
