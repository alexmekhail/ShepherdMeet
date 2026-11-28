import { act } from 'react';
import { createRoot } from 'react-dom/client';
import ThemeToggle from './ThemeToggle';

const originalMatchMedia = window.matchMedia;
let container;
let reactRoot;
let device;
let deviceChange;

beforeEach(() => {
  global.IS_REACT_ACT_ENVIRONMENT = true;
  container = document.createElement('div');
  document.body.append(container);
  reactRoot = createRoot(container);
  device = {
    matches: true,
    addEventListener: jest.fn((event, listener) => { deviceChange = listener; }),
    removeEventListener: jest.fn(),
  };
  window.matchMedia = jest.fn(() => device);
});

afterEach(() => {
  act(() => reactRoot.unmount());
  container.remove();
  delete document.documentElement.dataset.theme;
  delete document.documentElement.dataset.themePreference;
  localStorage.removeItem('shepherdmeet-theme');
  window.matchMedia = originalMatchMedia;
  deviceChange = undefined;
  delete global.IS_REACT_ACT_ENVIRONMENT;
});

test('follows device changes until the theme button is used and remembers the new theme', () => {
  const { dataset } = document.documentElement;
  act(() => reactRoot.render(<ThemeToggle />));
  const button = container.querySelector('button');
  expect(dataset.theme).toBe('dark');
  expect(button.getAttribute('aria-label')).toBe('Switch to light mode');

  device.matches = false;
  act(() => deviceChange());
  expect(dataset.theme).toBe('light');

  act(() => button.click());
  expect(device.removeEventListener).toHaveBeenCalledWith('change', deviceChange);
  expect(localStorage.getItem('shepherdmeet-theme')).toBe('dark');
  expect(dataset.theme).toBe('dark');

  act(() => button.click());
  expect(localStorage.getItem('shepherdmeet-theme')).toBe('light');
  expect(dataset.theme).toBe('light');
});

test('uses a saved manual choice over the device preference', () => {
  const { dataset } = document.documentElement;
  dataset.themePreference = 'light';
  act(() => reactRoot.render(<ThemeToggle />));
  expect(dataset.theme).toBe('light');
  expect(device.addEventListener).not.toHaveBeenCalled();
});

test('defaults to light when a device preference is unavailable', () => {
  window.matchMedia = undefined;
  act(() => reactRoot.render(<ThemeToggle />));
  expect(document.documentElement.dataset.theme).toBe('light');
});
