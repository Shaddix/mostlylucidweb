const puppeteer = require('puppeteer');

(async () => {
  const browser = await puppeteer.launch({ headless: false });
  const page = await browser.newPage();

  // Listen for console messages from the page
  page.on('console', msg => {
    console.log('PAGE LOG:', msg.text());
  });

  await page.setViewport({ width: 1920, height: 1080 });

  // Navigate to the site
  await page.goto('http://localhost:5000', { waitUntil: 'networkidle2' });

  // Wait for Alpine to load
  await new Promise(resolve => setTimeout(resolve, 2000));

  // Check Alpine and theme state
  const debug = await page.evaluate(() => {
    return {
      alpineLoaded: typeof Alpine !== 'undefined',
      globalSetupDefined: typeof window.globalSetup === 'function',
      bodyXData: document.body.getAttribute('x-data'),
      bodyXInit: document.body.getAttribute('x-init'),
      htmlClasses: document.documentElement.className,
      darkClassPresent: document.documentElement.classList.contains('dark'),
      lightClassPresent: document.documentElement.classList.contains('light'),
      localStorage: localStorage.theme,
      logoElement: document.querySelector('.img-filter-dark') !== null,
      logoParent: document.querySelector('.svg-container img')?.className || 'NOT FOUND',
      allLogos: Array.from(document.querySelectorAll('.svg-container img')).map(img => ({
        src: img.src,
        classes: img.className
      }))
    };
  });

  console.log('\n=== Debug Info ===');
  console.log(JSON.stringify(debug, null, 2));

  // Try to manually call themeInit if Alpine is loaded
  const manualInit = await page.evaluate(() => {
    if (typeof Alpine !== 'undefined' && Alpine.$data) {
      const bodyData = Alpine.$data(document.body);
      console.log('Body Alpine data:', bodyData);
      if (bodyData && typeof bodyData.themeInit === 'function') {
        bodyData.themeInit();
        return {
          success: true,
          afterInit: {
            htmlClasses: document.documentElement.className,
            isDarkMode: bodyData.isDarkMode
          }
        };
      }
    }
    return { success: false, error: 'Could not access Alpine data' };
  });

  console.log('\n=== Manual Init Result ===');
  console.log(JSON.stringify(manualInit, null, 2));

  await new Promise(resolve => setTimeout(resolve, 1000));
  await page.screenshot({ path: 'theme-debug.png', fullPage: false });
  console.log('\nScreenshot saved as theme-debug.png');

  await browser.close();
})();
