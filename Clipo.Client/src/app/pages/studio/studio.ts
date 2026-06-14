import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

interface Stat {
  label: string;
  value: string;
  icon: string;
}

@Component({
  selector: 'app-studio',
  imports: [RouterLink],
  templateUrl: './studio.html',
  styles: ``,
})
export class Studio {
  readonly stats: Stat[] = [
    { label: 'Total views',  value: '0', icon: 'pi-eye' },
    { label: 'Subscribers',  value: '0', icon: 'pi-users' },
    { label: 'Published',    value: '0', icon: 'pi-video' },
  ];
}
