import { Component, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Button } from 'primeng/button';
import { Avatar } from 'primeng/avatar';

@Component({
  selector: 'app-navbar',
  imports: [RouterLink, Button, Avatar],
  templateUrl: './navbar.html',
})
export class Navbar {
  readonly sidebarToggle = output<void>();
}
