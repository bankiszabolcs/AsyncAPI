import { Component, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { Ripple } from 'primeng/ripple';
import { Tooltip } from 'primeng/tooltip';
import { TranslocoPipe } from '@jsverse/transloco';

interface NavItem {
  label: string;
  icon: string;
  route: string;
  exact?: boolean;
}

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive, Ripple, Tooltip, TranslocoPipe],
  templateUrl: './sidebar.html',
})
export class Sidebar {
  readonly collapsed = input(false);
  readonly drawerMode = input(false);
  readonly itemClick = output<void>();

  readonly navItems: NavItem[] = [
    { label: 'nav.home',          icon: 'pi-home',    route: '/',              exact: true },
    { label: 'nav.explore',       icon: 'pi-compass', route: '/explore' },
    { label: 'nav.subscriptions', icon: 'pi-users',   route: '/subscriptions' },
    { label: 'nav.library',       icon: 'pi-book',    route: '/library' },
    { label: 'nav.history',       icon: 'pi-history', route: '/history' },
  ];
}
