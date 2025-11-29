import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <App />
)

// Service worker registration and update handling
if ('serviceWorker' in navigator) {
  const registerSw = async () => {
    try {
      const reg = await navigator.serviceWorker.register('/service-worker.js');

      // Tell SW to skip waiting when we manually request an update
      const tryUpdate = () => {
        if (!reg) return;
        if (reg.waiting) {
          reg.waiting.postMessage({ type: 'SKIP_WAITING' });
        } else if (reg.installing) {
          // no-op
        } else {
          reg.update();
        }
      };

      // When the page becomes visible, check for updates. iOS PWAs can be suspended; this helps.
      document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') {
          tryUpdate();
        }
      });

      // Periodic check every 5 minutes while page is open
      setInterval(() => reg.update(), 1000 * 60 * 5);

      // Reload to activate new SW when it takes control
      navigator.serviceWorker.addEventListener('controllerchange', () => {
        window.location.reload();
      });
    } catch (e) {
      // Registration failed - ignore
      // console.warn('SW registration failed', e)
    }
  };

  registerSw();
}
