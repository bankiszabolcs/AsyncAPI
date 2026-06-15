import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgTemplateOutlet } from '@angular/common';

interface SettingItem {
  label: string;
  description: string;
  icon: string;
  route?: string;
}

interface SettingGroup {
  title: string;
  items: SettingItem[];
}

@Component({
  selector: 'app-settings',
  imports: [RouterLink, NgTemplateOutlet],
  templateUrl: './settings.html',
  styles: ``,
})
export class Settings {
  readonly groups: SettingGroup[] = [
    {
      title: 'Account',
      items: [
        { label: 'Profile', description: 'Name, avatar and channel details', icon: 'pi-user', route: '/settings/profile' },
        { label: 'Privacy', description: 'Control who can see your activity', icon: 'pi-lock' },
      ],
    },
    {
      title: 'Playback',
      items: [
        { label: 'Quality', description: 'Default video quality and data usage', icon: 'pi-sliders-h' },
        { label: 'Autoplay', description: 'Play the next video automatically', icon: 'pi-play' },
      ],
    },
    {
      title: 'Notifications',
      items: [
        { label: 'Email', description: 'Updates and recommendations by email', icon: 'pi-envelope' },
        { label: 'Push', description: 'Activity from channels you follow', icon: 'pi-bell' },
      ],
    },
  ];
}
