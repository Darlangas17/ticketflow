import { DOCUMENT } from '@angular/common';
import { Injectable, computed, effect, inject, signal } from '@angular/core';

/** "system" follows the operating system setting; the others force a scheme. */
export type ThemeMode = 'system' | 'light' | 'dark';

export const THEME_STORAGE_KEY = 'ticketflow.theme';
const MODES: readonly ThemeMode[] = ['system', 'light', 'dark'];

/**
 * Light/dark theme. The Material tokens follow the CSS `color-scheme` property (see styles.scss),
 * so switching only sets that property on <html>; the choice is remembered in localStorage.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);

  readonly mode = signal<ThemeMode>(readStoredMode());

  readonly icon = computed(() => {
    switch (this.mode()) {
      case 'light':
        return 'light_mode';
      case 'dark':
        return 'dark_mode';
      default:
        return 'brightness_auto';
    }
  });

  /** Describes the current state and what the button does next (for aria-label/tooltip). */
  readonly label = computed(() => {
    const next = nextMode(this.mode());
    const current = { system: 'System theme', light: 'Light theme', dark: 'Dark theme' }[
      this.mode()
    ];
    return `${current}. Switch to ${next === 'system' ? 'system' : next} theme`;
  });

  constructor() {
    effect(() => {
      const mode = this.mode();
      this.document.documentElement.style.colorScheme = mode === 'system' ? '' : mode;
      try {
        localStorage.setItem(THEME_STORAGE_KEY, mode);
      } catch {
        // Storage unavailable: the choice lasts for this visit only.
      }
    });
  }

  /** system → light → dark → system. */
  cycle(): void {
    this.mode.update(nextMode);
  }
}

function nextMode(mode: ThemeMode): ThemeMode {
  return MODES[(MODES.indexOf(mode) + 1) % MODES.length]!;
}

function readStoredMode(): ThemeMode {
  try {
    const stored = localStorage.getItem(THEME_STORAGE_KEY);
    return MODES.find((mode) => mode === stored) ?? 'system';
  } catch {
    return 'system';
  }
}
