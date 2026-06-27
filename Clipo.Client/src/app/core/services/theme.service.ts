import { effect, inject, Injectable, signal } from '@angular/core';
import { DOCUMENT } from '@angular/common';

export type ColorTheme = 'dark' | 'light';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly doc = inject(DOCUMENT);

  readonly theme = signal<ColorTheme>(
    (localStorage.getItem('clipo_theme') as ColorTheme) ?? 'dark'
  );

  constructor() {
    effect(() => {
      const t = this.theme();
      const el = this.doc.documentElement;
      if (t === 'dark') {
        el.classList.add('dark');
        el.classList.remove('light');
      } else {
        el.classList.remove('dark');
        el.classList.add('light');
      }
      localStorage.setItem('clipo_theme', t);
    });
  }

  setTheme(theme: ColorTheme): void {
    this.theme.set(theme);
  }
}
