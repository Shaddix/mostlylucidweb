const puppeteer = require('puppeteer');

(async () => {
  const browser = await puppeteer.launch({ headless: false });
  const page = await browser.newPage();

  // Capture all console messages
  page.on('console', msg => {
    const type = msg.type();
    const text = msg.text();
    console.log(`[${type.toUpperCase()}]`, text);
  });

  // Capture page errors
  page.on('pageerror', error => {
    console.log('[PAGE ERROR]', error.message);
  });

  // Capture failed requests
  page.on('requestfailed', request => {
    console.log('[REQUEST FAILED]', request.url());
  });

  // Capture response errors
  page.on('response', response => {
    if (response.status() >= 400) {
      console.log(`[HTTP ${response.status()}]`, response.url());
    }
  });

  await page.setViewport({ width: 1920, height: 1080 });

  // Navigate
  console.log('Navigating to http://localhost:5000...\n');
  await page.goto('http://localhost:5000', { waitUntil: 'networkidle2' });

  // Wait a bit
  await new Promise(resolve => setTimeout(resolve, 3000));

  // Check what's on window
  const windowCheck = await page.evaluate(() => {
    const keys = Object.keys(window).filter(k => k.includes('global') || k.includes('Setup') || k.includes('mostlylucid'));
    return {
      matchingKeys: keys,
      hasGlobalSetup: 'globalSetup' in window,
      typeofGlobalSetup: typeof window.globalSetup,
      hasMostlylucid: 'mostlylucid' in window,
      mostlylucidKeys: window.mostlylucid ? Object.keys(window.mostlylucid) : []
    };
  });

  console.log('\n=== Window Object Check ===');
  console.log(JSON.stringify(windowCheck, null, 2));

  await browser.close();
})();
