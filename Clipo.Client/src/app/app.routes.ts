import { Routes } from '@angular/router';
import { VideoService } from './core/services/video.service';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/home/home').then(m => m.Home),
    providers: [VideoService],
  },
  {
    path: 'upload',
    loadComponent: () => import('./pages/upload/upload').then(m => m.Upload),
  },
  {
    path: 'explore',
    loadComponent: () => import('./pages/explore/explore').then(m => m.Explore),
  },
  {
    path: 'subscriptions',
    loadComponent: () => import('./pages/subscriptions/subscriptions').then(m => m.Subscriptions),
  },
  {
    path: 'library',
    loadComponent: () => import('./pages/library/library').then(m => m.Library),
  },
  {
    path: 'history',
    loadComponent: () => import('./pages/history/history').then(m => m.History),
  },
  {
    path: 'channel',
    loadComponent: () => import('./pages/channel/channel').then(m => m.Channel),
  },
  {
    path: 'studio',
    loadComponent: () => import('./pages/studio/studio').then(m => m.Studio),
  },
  {
    path: 'settings',
    loadComponent: () => import('./pages/settings/settings').then(m => m.Settings),
  },
  {
    path: 'watch/:id',
    loadComponent: () => import('./pages/watch/watch').then(m => m.Watch),
    providers: [VideoService],
  },
];
