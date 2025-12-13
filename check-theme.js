const puppeteer = require('puppeteer');
const path = require('path');

(async () => {
  const browser = await puppeteer.launch({ headless: false });
  const page = await browser.newPage();

  await page.setViewport({ width: 1920, height: 1080 });

  // Navigate to the site
  await page.goto('http://localhost:5000', { waitUntil: 'networkidle2' });

  // Take initial screenshot
  await page.screenshot({ path: 'theme-initial.png', fullPage: false });
  console.log('Screenshot 1: Initial state saved as theme-initial.png');

  // Check current theme
  const currentTheme = await page.evaluate(() => {
    return {
      htmlClass: document.documentElement.className,
      isDarkMode: document.documentElement.classList.contains('dark'),
      localStorage: localStorage.theme,
      logoClasses: document.querySelector('.img-filter-dark')?.className || 'NOT FOUND'
    };
  });

  console.log('Current theme state:', currentTheme);

  // Wait a moment
  await new Promise(resolve => setTimeout(resolve, 1000));

  // Click the theme toggle button
  await page.evaluate(() => {
    const button = document.querySelector('[x-on\\:click="themeSwitch()"]');
    if (button) {
      button.click();
    }
  });

  // Wait for theme to change
  await new Promise(resolve => setTimeout(resolve, 500));

  // Take screenshot after toggle
  await page.screenshot({ path: 'theme-toggled.png', fullPage: false });
  console.log('Screenshot 2: After toggle saved as theme-toggled.png');

  // Check theme after toggle
  const afterToggle = await page.evaluate(() => {
    return {
      htmlClass: document.documentElement.className,
      isDarkMode: document.documentElement.classList.contains('dark'),
      localStorage: localStorage.theme,
      logoClasses: document.querySelector('.img-filter-dark')?.className || 'NOT FOUND'
    };
  });

  console.log('After toggle theme state:', afterToggle);

  await browser.close();
})();
