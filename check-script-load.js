const puppeteer = require('puppeteer');

(async () => {
  const browser = await puppeteer.launch({ headless: false });
  const page = await browser.newPage();

  // Listen for console messages
  page.on('console', msg => console.log('PAGE:', msg.text()));

  await page.setViewport({ width: 1920, height: 1080 });

  // Check at different stages
  page.on('domcontentloaded', async () => {
    const state = await page.evaluate(() => ({
      stage: 'DOMContentLoaded',
      globalSetup: typeof window.globalSetup,
      Alpine: typeof Alpine
    }));
    console.log('DOM Loaded:', state);
  });

  page.on('load', async () => {
    const state = await page.evaluate(() => ({
      stage: 'Load Event',
      globalSetup: typeof window.globalSetup,
      Alpine: typeof Alpine
    }));
    console.log('Page Loaded:', state);
  });

  // Navigate
  await page.goto('http://localhost:5000', { waitUntil: 'networkidle2' });

  // Check after everything
  await new Promise(resolve => setTimeout(resolve, 3000));

  const finalState = await page.evaluate(() => {
    const bodyAttrs = {
      xData: document.body.getAttribute('x-data'),
      xInit: document.body.getAttribute('x-init')
    };

    const scripts = Array.from(document.querySelectorAll('script')).map(s => ({
      src: s.src.substring(s.src.lastIndexOf('/') + 1),
      type: s.type || 'default',
      loaded: s.src ? 'external' : 'inline'
    }));

    return {
      stage: 'Final Check (3s after load)',
      globalSetup: typeof window.globalSetup,
      Alpine: typeof Alpine,
      alpineStarted: typeof Alpine !== 'undefined' ? (Alpine.version || 'started') : 'N/A',
      bodyAttrs,
      mainJsFound: scripts.find(s => s.src.includes('main.js')),
      htmlClass: document.documentElement.className
    };
  });

  console.log('\n=== Final State ===');
  console.log(JSON.stringify(finalState, null, 2));

  await browser.close();
})();
