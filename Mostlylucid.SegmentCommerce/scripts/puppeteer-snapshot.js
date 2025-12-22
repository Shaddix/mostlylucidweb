const fs = require('fs');
const path = require('path');
const puppeteer = require('puppeteer');

(async () => {
  const baseUrl = process.env.BASE_URL || 'http://localhost:8082';
  const outDir = path.join(__dirname, 'screenshots');
  const outFile = path.join(outDir, 'home.png');

  if (!fs.existsSync(outDir)) {
    fs.mkdirSync(outDir, { recursive: true });
  }

  const browser = await puppeteer.launch({ headless: 'new' });
  const page = await browser.newPage();
  page.setViewport({ width: 1400, height: 900 });

  await page.goto(baseUrl, { waitUntil: 'networkidle2', timeout: 30000 });

  // Wait for product cards to render (if any)
  await page.waitForTimeout(2000);

  await page.screenshot({ path: outFile, fullPage: true });
  console.log(`Saved screenshot to ${outFile}`);

  await browser.close();
})();
