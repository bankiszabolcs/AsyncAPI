import { Routes } from '@angular/router';
import { VideoService } from './core/services/video.service';
import { authGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./pages/home/home').then(m => m.Home),
    providers: [VideoService],
  },
  {
    path: 'explore',
    loadComponent: () => import('./pages/explore/explore').then(m => m.Explore),
  },
  {
    path: 'watch/:id',
    loadComponent: () => import('./pages/watch/watch').then(m => m.Watch),
    providers: [VideoService],
  },
  {
    path: 'upload',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/upload/upload').then(m => m.Upload),
  },
  {
    path: 'subscriptions',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/subscriptions/subscriptions').then(m => m.Subscriptions),
  },
  {
    path: 'library',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/library/library').then(m => m.Library),
  },
  {
    path: 'history',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/history/history').then(m => m.History),
  },
  {
    path: 'channel',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/channel/channel').then(m => m.Channel),
    providers: [VideoService],
  },
  {
    path: 'studio',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/studio/studio').then(m => m.Studio),
    providers: [VideoService],
  },
  {
    path: 'settings',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/settings/settings').then(m => m.Settings),
  },
  {
    path: 'settings/profile',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/settings/profile/profile').then(m => m.Profile),
  },
];
