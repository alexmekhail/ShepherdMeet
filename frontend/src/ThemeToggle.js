import { useLayoutEffect, useState } from 'react';

const root = document.documentElement;

export default function ThemeToggle() {
  const [preference, setPreference] = useState(() => root.dataset.themePreference);
  const [deviceTheme, setDeviceTheme] = useState(() => root.dataset.theme || 'light');
  const theme = preference || deviceTheme;
  const nextTheme = theme === 'dark' ? 'light' : 'dark';
  const label = `Switch to ${nextTheme} mode`;

  useLayoutEffect(() => {
    if (preference) return;
    const device = window.matchMedia?.('(prefers-color-scheme: dark)');

    function syncDeviceTheme() {
      setDeviceTheme(device?.matches ? 'dark' : 'light');
    }

    syncDeviceTheme();
    if (device?.addEventListener) {
      device.addEventListener('change', syncDeviceTheme);
      return () => device.removeEventListener('change', syncDeviceTheme);
    }
    device?.addListener?.(syncDeviceTheme);
    return () => device?.removeListener?.(syncDeviceTheme);
  }, [preference]);

  useLayoutEffect(() => {
    root.dataset.theme = theme;
    const canvas = getComputedStyle(root).getPropertyValue('--bg-page').trim();
    if (canvas) document.querySelector('meta[name="theme-color"]')?.setAttribute('content', canvas);
  }, [theme]);

  function toggleTheme() {
    setPreference(nextTheme);
    root.dataset.themePreference = nextTheme;
    try {
      localStorage.setItem('shepherdmeet-theme', nextTheme);
    } catch {
      // The current page can still switch themes when storage is unavailable.
    }
  }

  return (
    <button className="secondary theme-toggle" type="button" onClick={toggleTheme}
      aria-label={label} title={label}>
      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor"
        strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" focusable="false">
        {nextTheme === 'light' ? <>
          <circle cx="12" cy="12" r="4" />
          <path d="M12 2.5v2m0 15v2M2.5 12h2m15 0h2M5.3 5.3l1.4 1.4m10.6 10.6 1.4 1.4m0-13.4-1.4 1.4M6.7 17.3l-1.4 1.4" />
        </> : <path d="M20.7 14.1a8.8 8.8 0 0 1-10.8-10.8 8.8 8.8 0 1 0 10.8 10.8Z" />}
      </svg>
    </button>
  );
}
