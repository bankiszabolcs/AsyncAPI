import { Component, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { Ripple } from 'primeng/ripple';
import { Tooltip } from 'primeng/tooltip';

interface NavItem {
  label: string;
  icon: string;
  route: string;
  exact?: boolean;
}

@Component({
  selector: 'app-sidebar',
  imports: [RouterLink, RouterLinkActive, Ripple, Tooltip],
  templateUrl: './sidebar.html',
})
export class Sidebar {
  readonly collapsed = input(false);
  // Drawerben (mobil) teljes szélességgel, saját háttér/árnyék nélkül jelenik meg.
  readonly drawerMode = input(false);
  // Menüpontra kattintva a szülő bezárhatja a mobil drawert.
  readonly itemClick = output<void>();

  readonly navItems: NavItem[] = [
    { label: 'Főoldal',        icon: 'pi-home',      route: '/',              exact: true },
    { label: 'Felfedezés',     icon: 'pi-compass',   route: '/explore' },
    { label: 'Feliratkozások', icon: 'pi-users',      route: '/subscriptions' },
    { label: 'Könyvtár',       icon: 'pi-book',       route: '/library' },
    { label: 'Előzmények',     icon: 'pi-history',    route: '/history' },
  ];
}
