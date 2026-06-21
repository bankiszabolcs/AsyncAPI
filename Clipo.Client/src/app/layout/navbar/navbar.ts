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
import { SearchBar } from '../../shared/search-bar/search-bar';
import { VideoService } from '../../core/services/video.service';
import { NotificationService } from '../../core/services/notification.service';
import { AppNotification } from '../../core/models/notification.model';
import { TimeAgoPipe } from '../../shared/pipes/time-ago.pipe';

@Component({
  selector: 'app-navbar',
  imports: [RouterLink, NgTemplateOutlet, Button, Avatar, Menu, Drawer, Popover, Ripple, SearchBar, TimeAgoPipe],
  providers: [VideoService],
  templateUrl: './navbar.html',
})
export class Navbar {
  readonly sidebarToggle = output<void>();
  readonly auth     = inject(AuthService);
  readonly notifSvc = inject(NotificationService);

  readonly isMobile          = signal(this.checkMobile());
  readonly drawerVisible     = signal(false);
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

  onBellClick(popover: Popover, event: Event): void {
    this.notifSvc.loadNotifications();
    if (this.isMobile()) {
      this.notifDrawerVisible.set(true);
    } else {
      popover.toggle(event);
    }
  }

  onNotifClick(n: AppNotification, popover?: Popover): void {
    popover?.hide();
    this.notifDrawerVisible.set(false);
    this.notifSvc.onNotificationRead(n.id);
    if (n.link) this.notifSvc.navigateTo(n.link);
  }

  onDrawerItemClick(item: MenuItem): void {
    item.command?.({ originalEvent: undefined as never, item });
    this.drawerVisible.set(false);
  }

  notifIcon(type: string): string {
    switch (type) {
      case 'new_video':        return 'pi-video';
      case 'new_comment':      return 'pi-comment';
      case 'comment_reply':    return 'pi-reply';
      case 'video_processed':  return 'pi-check-circle';
      default:                 return 'pi-bell';
    }
  }

  private logout(): void { this.auth.logout(); }

  private checkMobile(): boolean {
    return typeof window !== 'undefined' && window.innerWidth < 1280;
  }
}
