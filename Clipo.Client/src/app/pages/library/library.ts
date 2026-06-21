import { Component } from '@angular/core';
import { Tabs, TabList, Tab, TabPanels, TabPanel } from 'primeng/tabs';
import { LibraryPlaylists } from './playlists/library-playlists';

@Component({
  selector: 'app-library',
  imports: [Tabs, TabList, Tab, TabPanels, TabPanel, LibraryPlaylists],
  templateUrl: './library.html',
  styles: ``,
})
export class Library {}
