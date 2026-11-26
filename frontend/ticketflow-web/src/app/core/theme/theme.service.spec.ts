import { TestBed } from '@angular/core/testing';

import { THEME_STORAGE_KEY, ThemeService } from './theme.service';

describe('ThemeService', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.style.colorScheme = '';
  });

  function create(): ThemeService {
    const service = TestBed.inject(ThemeService);
    TestBed.tick(); // run the effect that applies the mode
    return service;
  }

  it('follows the system by default', () => {
    const theme = create();

    expect(theme.mode()).toBe('system');
    expect(document.documentElement.style.colorScheme).toBe('');
    expect(theme.icon()).toBe('brightness_auto');
  });

  it('cycles system → light → dark → system and applies the color scheme', () => {
    const theme = create();

    theme.cycle();
    TestBed.tick();
    expect(document.documentElement.style.colorScheme).toBe('light');

    theme.cycle();
    TestBed.tick();
    expect(document.documentElement.style.colorScheme).toBe('dark');
    expect(theme.icon()).toBe('dark_mode');

    theme.cycle();
    TestBed.tick();
    expect(theme.mode()).toBe('system');
  });

  it('remembers the choice', () => {
    const theme = create();
    theme.cycle();
    TestBed.tick();

    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light');
  });

  it('restores a saved choice and ignores invalid values', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark');
    expect(create().mode()).toBe('dark');

    TestBed.resetTestingModule();
    localStorage.setItem(THEME_STORAGE_KEY, 'purple');
    expect(create().mode()).toBe('system');
  });

  it('describes the current state and the next action for screen readers', () => {
    const theme = create();

    expect(theme.label()).toBe('System theme. Switch to light theme');
  });
});
