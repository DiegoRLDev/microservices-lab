import { DestroyRef, Injectable, inject, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark' | 'auto';
export interface Appearance { mode: ThemeMode; accent: string; }

declare global {
  interface Window {
    mlabTheme: {
      load(): Appearance;
      apply(value: Appearance, systemDark: boolean): Appearance;
      save(value: Appearance): void;
    };
  }
}

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly media = window.matchMedia?.('(prefers-color-scheme: dark)');
  private readonly state = signal(window.mlabTheme.load());
  readonly preferences = this.state.asReadonly();
  readonly modes: ReadonlyArray<{ value: ThemeMode; label: string }> = [
    { value: 'light', label: 'Claro' }, { value: 'dark', label: 'Escuro' }, { value: 'auto', label: 'Automatico' }
  ];
  readonly accents = [
    { label: 'Verde', value: '#0f766e' }, { label: 'Azul', value: '#2563eb' },
    { label: 'Roxo', value: '#7c3aed' }, { label: 'Laranja', value: '#ea580c' },
    { label: 'Vermelho', value: '#dc2626' }, { label: 'Ciano', value: '#0891b2' },
    { label: 'Amarelo', value: '#eab308' }, { label: 'Rosa', value: '#ec4899' },
    { label: 'Grafite', value: '#374151' }
  ];

  constructor() {
    const updateSystem = () => this.apply();
    this.media?.addEventListener('change', updateSystem);
    inject(DestroyRef).onDestroy(() => this.media?.removeEventListener('change', updateSystem));
    this.apply();
  }

  setMode(mode: ThemeMode) { this.update({ ...this.state(), mode }); }
  setAccent(accent: string) {
    if (/^#[0-9a-f]{6}$/i.test(accent)) this.update({ ...this.state(), accent });
  }

  private update(value: Appearance) {
    this.state.set(window.mlabTheme.apply(value, this.media?.matches ?? false));
    window.mlabTheme.save(this.state());
  }

  private apply() { window.mlabTheme.apply(this.state(), this.media?.matches ?? false); }
}
