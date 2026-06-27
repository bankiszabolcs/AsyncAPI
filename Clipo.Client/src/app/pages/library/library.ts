import { Component } from '@angular/core';
import { Tabs, TabList, Tab, TabPanels, TabPanel } from 'primeng/tabs';
import { LibraryPlaylists } from './playlists/library-playlists';
import { LibrarySavedVideos } from './saved-videos/library-saved-videos';
import { LibraryWatchLater } from './watch-later/library-watch-later';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-library',
  imports: [Tabs, TabList, Tab, TabPanels, TabPanel, LibraryPlaylists, LibrarySavedVideos, LibraryWatchLater, TranslocoPipe],
  templateUrl: './library.html',
  styles: `
    :host ::ng-deep .p-tabpanels,
    :host ::ng-deep .p-tablist {
      background: transparent;
    }
  `,
})
export class Library {}
