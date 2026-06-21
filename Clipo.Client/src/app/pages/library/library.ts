import { Component } from '@angular/core';
import { Tabs, TabList, Tab, TabPanels, TabPanel } from 'primeng/tabs';
import { LibraryPlaylists } from './playlists/library-playlists';
import { LibrarySavedVideos } from './saved-videos/library-saved-videos';

@Component({
  selector: 'app-library',
  imports: [Tabs, TabList, Tab, TabPanels, TabPanel, LibraryPlaylists, LibrarySavedVideos],
  templateUrl: './library.html',
  styles: ``,
})
export class Library {}
