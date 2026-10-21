import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import bootstrap from '../../../public/theme.js?raw';
import index from '../../index.html?raw';
import { ThemeService } from './theme.service';

const storageKey = 'microserviceslab.appearance';
const start = () => new Function('window', 'document', bootstrap)(window, document);

function contrast(a: string, b: string) {
  const luminance = (hex: string) => {
    const rgb = [1, 3, 5].map((offset) => {
      const value = parseInt(hex.slice(offset, offset + 2), 16) / 255;
      return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
    });
    return rgb[0] * 0.2126 + rgb[1] * 0.7152 + rgb[2] * 0.0722;
  };
  const values = [luminance(a), luminance(b)];
  return (Math.max(...values) + 0.05) / (Math.min(...values) + 0.05);
}

describe('Appearance bootstrap and ThemeService', () => {
  let media: { matches: boolean; addEventListener: ReturnType<typeof vi.fn>; removeEventListener: ReturnType<typeof vi.fn> };
  let change: () => void;
  const token = (name: string) => document.documentElement.style.getPropertyValue(name);
  const system = (dark: boolean) => { media.matches = dark; change(); };

  beforeEach(() => {
    localStorage.clear();
    media = { matches: false, addEventListener: vi.fn((_name, handler) => { change = handler; }), removeEventListener: vi.fn() };
    vi.stubGlobal('matchMedia', vi.fn(() => media));
    start();
  });

  afterEach(() => {
    TestBed.resetTestingModule();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    localStorage.clear();
    start();
  });

  it('loads automatic mode and the existing green by default', () => {
    const service = TestBed.inject(ThemeService);
    expect(service.preferences()).toEqual({ mode: 'auto', accent: '#0f766e' });
    expect(document.documentElement.dataset['theme']).toBe('light');
    expect(token('--color-primary-strong')).toBe('#115e59');
  });

  it('restores and applies preferences before Angular starts', () => {
    localStorage.setItem(storageKey, JSON.stringify({ mode: 'dark', accent: '#2563eb' }));
    start();
    expect(document.documentElement.dataset['theme']).toBe('dark');
    expect(token('--color-primary')).toBe('#2563eb');
    expect(index.indexOf('<script src="theme.js"></script>')).toBeLessThan(index.indexOf('</head>'));
    expect(TestBed.inject(ThemeService).preferences()).toEqual({ mode: 'dark', accent: '#2563eb' });
  });

  it('switches modes and follows system changes only in automatic mode', () => {
    const service = TestBed.inject(ThemeService);
    system(true);
    expect(document.documentElement.dataset['theme']).toBe('dark');
    service.setMode('light');
    system(true);
    expect(document.documentElement.dataset['theme']).toBe('light');
    service.setMode('dark');
    system(false);
    expect(document.documentElement.dataset['theme']).toBe('dark');
    service.setMode('auto');
    expect(document.documentElement.dataset['theme']).toBe('light');
    system(true);
    expect(document.documentElement.dataset['theme']).toBe('dark');
  });

  it('persists both preferences and restores the same choice on reload', () => {
    const service = TestBed.inject(ThemeService);
    service.setMode('dark');
    service.setAccent('#ABCDEF');
    expect(JSON.parse(localStorage.getItem(storageKey)!)).toEqual({ mode: 'dark', accent: '#abcdef' });
    start();
    expect(window.mlabTheme.load()).toEqual(service.preferences());
    expect(token('--color-primary')).toBe('#abcdef');
  });

  it.each(['broken JSON', 'null', '{"mode":"invalid","accent":"red"}', '{"mode":"dark","accent":"url(bad)"}'])(
    'handles invalid stored preferences: %s', (stored) => {
      localStorage.setItem(storageKey, stored);
      start();
      expect(token('--color-primary')).toBe('#0f766e');
      expect(['auto', 'dark']).toContain(window.mlabTheme.load().mode);
    }
  );

  it('keeps working when localStorage cannot be read or written', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('blocked'); });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('quota'); });
    start();
    const service = TestBed.inject(ThemeService);
    expect(() => { service.setMode('dark'); service.setAccent('#ffffff'); }).not.toThrow();
    expect(service.preferences()).toEqual({ mode: 'dark', accent: '#ffffff' });
    expect(document.documentElement.dataset['theme']).toBe('dark');
  });

  it('ignores invalid custom colors without changing the saved preference', () => {
    const service = TestBed.inject(ThemeService);
    service.setAccent('#2563eb');
    service.setAccent('#xyxyxy');
    expect(service.preferences().accent).toBe('#2563eb');
    expect(window.mlabTheme.load().accent).toBe('#2563eb');
  });

  it('provides nine named presets while preserving the original six', () => {
    expect(TestBed.inject(ThemeService).accents).toEqual([
      { label: 'Verde', value: '#0f766e' }, { label: 'Azul', value: '#2563eb' },
      { label: 'Roxo', value: '#7c3aed' }, { label: 'Laranja', value: '#ea580c' },
      { label: 'Vermelho', value: '#dc2626' }, { label: 'Ciano', value: '#0891b2' },
      { label: 'Amarelo', value: '#eab308' }, { label: 'Rosa', value: '#ec4899' },
      { label: 'Grafite', value: '#374151' }
    ]);
  });

  it.each(['#eab308', '#ec4899', '#374151'])('selects and restores %s in both themes', (accent) => {
    const service = TestBed.inject(ThemeService);
    for (const mode of ['light', 'dark'] as const) {
      service.setMode(mode);
      service.setAccent(accent);
      expect(service.preferences()).toEqual({ mode, accent });
      expect(JSON.parse(localStorage.getItem(storageKey)!)).toEqual({ mode, accent });
      start();
      expect(window.mlabTheme.load()).toEqual({ mode, accent });
      expect(document.documentElement.dataset['theme']).toBe(mode);
      expect(token('--color-primary')).toBe(accent);
    }
  });

  it('lightens graphite text and focus in dark mode without changing the selected color', () => {
    const service = TestBed.inject(ThemeService);
    service.setMode('dark');
    service.setAccent('#374151');
    expect(token('--color-primary-strong')).not.toBe('#374151');
    expect(token('--color-accent')).toBe(token('--color-primary-strong'));
    expect(contrast(token('--color-accent'), '#202225')).toBeGreaterThanOrEqual(4.5);
    expect(window.mlabTheme.load().accent).toBe('#374151');
  });

  it.each(['light', 'dark'] as const)('keeps custom button, link and soft-highlight text at 4.5:1 in %s mode', (mode) => {
    const service = TestBed.inject(ThemeService);
    service.setMode(mode);
    const colors = [...service.accents.map((accent) => accent.value), '#ffffff', '#000000', '#ffff00', '#777777', '#abcdef'];
    for (const color of colors) {
      service.setAccent(color);
      expect(token('--color-primary')).toBe(color);
      expect(contrast(token('--color-on-primary'), color)).toBeGreaterThanOrEqual(4.5);
      for (const bg of [mode === 'dark' ? '#202225' : '#ffffff', mode === 'dark' ? '#161819' : '#f6f7f9', token('--color-primary-soft')]) {
        expect(contrast(token('--color-primary-strong'), bg)).toBeGreaterThanOrEqual(4.5);
      }
    }
  });

  it('removes the system preference listener when destroyed', () => {
    TestBed.inject(ThemeService);
    TestBed.resetTestingModule();
    expect(media.removeEventListener).toHaveBeenCalledWith('change', change);
  });
});
