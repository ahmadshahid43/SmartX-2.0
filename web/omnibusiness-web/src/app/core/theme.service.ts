import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark';

const themeStorageKey = 'omnibusiness.theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);

  readonly theme = signal<ThemeMode>(this.loadTheme());

  constructor() {
    this.applyTheme(this.theme());
  }

  toggleTheme(): void {
    this.setTheme(this.theme() === 'dark' ? 'light' : 'dark');
  }

  setTheme(theme: ThemeMode): void {
    this.theme.set(theme);
    localStorage.setItem(themeStorageKey, theme);
    this.applyTheme(theme);
  }

  private loadTheme(): ThemeMode {
    return localStorage.getItem(themeStorageKey) === 'dark' ? 'dark' : 'light';
  }

  private applyTheme(theme: ThemeMode): void {
    this.document.documentElement.setAttribute('data-theme', theme);
  }
}
