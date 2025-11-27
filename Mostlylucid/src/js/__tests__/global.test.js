import { describe, it, expect, beforeEach, vi } from 'vitest';
import { globalSetup } from '../global';

describe('Global Theme Setup', () => {
  let themeInstance;

  beforeEach(() => {
    // Reset DOM
    document.documentElement.className = '';
    document.body.innerHTML = '<div id="light-mode"></div><div id="dark-mode"></div>';

    // Mock stylesheets
    document.getElementById('light-mode').disabled = false;
    document.getElementById('dark-mode').disabled = false;

    // Reset localStorage
    localStorage.clear();

    // Create fresh instance
    themeInstance = globalSetup();
  });

  describe('themeInit', () => {
    it('should initialize dark theme when localStorage has dark', () => {
      localStorage.theme = 'dark';
      const eventSpy = vi.fn();
      document.body.addEventListener('dark-theme-set', eventSpy);

      themeInstance.themeInit();

      expect(document.documentElement.classList.contains('dark')).toBe(true);
      expect(document.documentElement.classList.contains('light')).toBe(false);
      expect(themeInstance.isDarkMode).toBe(true);
      expect(localStorage.theme).toBe('dark');
      expect(eventSpy).toHaveBeenCalledOnce();
    });

    it('should initialize light theme when localStorage has light', () => {
      localStorage.theme = 'light';
      const eventSpy = vi.fn();
      document.body.addEventListener('light-theme-set', eventSpy);

      themeInstance.themeInit();

      expect(document.documentElement.classList.contains('light')).toBe(true);
      expect(document.documentElement.classList.contains('dark')).toBe(false);
      expect(themeInstance.isDarkMode).toBe(false);
      expect(localStorage.theme).toBe('base');
      expect(eventSpy).toHaveBeenCalledOnce();
    });

    it('should respect system preference when no localStorage theme', () => {
      // Mock system dark mode preference
      window.matchMedia = vi.fn().mockImplementation(query => ({
        matches: query === '(prefers-color-scheme: dark)',
        media: query,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      }));

      const eventSpy = vi.fn();
      document.body.addEventListener('dark-theme-set', eventSpy);

      themeInstance.themeInit();

      expect(document.documentElement.classList.contains('dark')).toBe(true);
      expect(themeInstance.isDarkMode).toBe(true);
      expect(eventSpy).toHaveBeenCalledOnce();
    });

    it('should apply correct stylesheets for dark mode', () => {
      localStorage.theme = 'dark';
      themeInstance.themeInit();

      const lightStylesheet = document.getElementById('light-mode');
      const darkStylesheet = document.getElementById('dark-mode');

      expect(lightStylesheet.disabled).toBe(true);
      expect(darkStylesheet.disabled).toBe(false);
    });

    it('should apply correct stylesheets for light mode', () => {
      localStorage.theme = 'light';
      themeInstance.themeInit();

      const lightStylesheet = document.getElementById('light-mode');
      const darkStylesheet = document.getElementById('dark-mode');

      expect(lightStylesheet.disabled).toBe(false);
      expect(darkStylesheet.disabled).toBe(true);
    });
  });

  describe('themeSwitch', () => {
    it('should switch from dark to light', () => {
      localStorage.theme = 'dark';
      themeInstance.isDarkMode = true;
      document.documentElement.classList.add('dark');

      const eventSpy = vi.fn();
      document.body.addEventListener('light-theme-set', eventSpy);

      themeInstance.themeSwitch();

      expect(localStorage.theme).toBe('light');
      expect(document.documentElement.classList.contains('light')).toBe(true);
      expect(document.documentElement.classList.contains('dark')).toBe(false);
      expect(themeInstance.isDarkMode).toBe(false);
      expect(eventSpy).toHaveBeenCalledOnce();
    });

    it('should switch from light to dark', () => {
      localStorage.theme = 'light';
      themeInstance.isDarkMode = false;
      document.documentElement.classList.add('light');

      const eventSpy = vi.fn();
      document.body.addEventListener('dark-theme-set', eventSpy);

      themeInstance.themeSwitch();

      expect(localStorage.theme).toBe('dark');
      expect(document.documentElement.classList.contains('dark')).toBe(true);
      expect(document.documentElement.classList.contains('light')).toBe(false);
      expect(themeInstance.isDarkMode).toBe(true);
      expect(eventSpy).toHaveBeenCalledOnce();
    });

    it('should update stylesheets when switching themes', () => {
      localStorage.theme = 'light';
      themeInstance.isDarkMode = false;

      themeInstance.themeSwitch();

      const lightStylesheet = document.getElementById('light-mode');
      const darkStylesheet = document.getElementById('dark-mode');

      expect(lightStylesheet.disabled).toBe(true);
      expect(darkStylesheet.disabled).toBe(false);
    });
  });

  describe('Event dispatching', () => {
    it('should dispatch CustomEvent with correct type for dark theme', () => {
      localStorage.theme = 'dark';

      const eventPromise = new Promise(resolve => {
        document.body.addEventListener('dark-theme-set', (e) => {
          expect(e).toBeInstanceOf(CustomEvent);
          expect(e.type).toBe('dark-theme-set');
          resolve();
        });
      });

      themeInstance.themeInit();

      return eventPromise;
    });

    it('should dispatch CustomEvent with correct type for light theme', () => {
      localStorage.theme = 'light';

      const eventPromise = new Promise(resolve => {
        document.body.addEventListener('light-theme-set', (e) => {
          expect(e).toBeInstanceOf(CustomEvent);
          expect(e.type).toBe('light-theme-set');
          resolve();
        });
      });

      themeInstance.themeInit();

      return eventPromise;
    });
  });
});
