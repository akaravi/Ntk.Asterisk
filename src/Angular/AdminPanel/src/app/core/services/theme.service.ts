import { Injectable, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'ntk.theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly mode = signal<ThemeMode>(this.readStored());

  constructor() {
    this.apply(this.mode());
    if (typeof window !== 'undefined' && window.matchMedia) {
      window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
        if (this.mode() === 'system') this.apply('system');
      });
    }
  }

  setMode(mode: ThemeMode): void {
    this.mode.set(mode);
    try {
      localStorage.setItem(STORAGE_KEY, mode);
    } catch {
      /* ignore */
    }
    this.apply(mode);
  }

  cycle(): void {
    const order: ThemeMode[] = ['light', 'dark', 'system'];
    const i = order.indexOf(this.mode());
    this.setMode(order[(i + 1) % order.length]);
  }

  private readStored(): ThemeMode {
    try {
      const v = localStorage.getItem(STORAGE_KEY);
      if (v === 'light' || v === 'dark' || v === 'system') return v;
    } catch {
      /* ignore */
    }
    return 'system';
  }

  private apply(mode: ThemeMode): void {
    const root = document.documentElement;
    if (mode === 'system') {
      root.removeAttribute('data-theme');
      root.style.colorScheme = '';
      return;
    }
    root.setAttribute('data-theme', mode);
    root.style.colorScheme = mode;
  }
}
