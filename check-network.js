const puppeteer = require('puppeteer');

(async () => {
  const browser = await puppeteer.launch({ headless: false });
  const page = await browser.newPage();

  const jsFiles = [];

  page.on('response', async (response) => {
    const url = response.url();
    if (url.includes('.js')) {
      jsFiles.push({
        url: url.substring(url.lastIndexOf('/') + 1),
        status: response.status(),
        type: response.headers()['content-type']
      });
    }
  });

  await page.setViewport({ width: 1920, height: 1080 });
  await page.goto('http://localhost:5000', { waitUntil: 'networkidle2' });

  await new Promise(resolve => setTimeout(resolve, 2000));

  console.log('\n=== JS Files Loaded ===');
  jsFiles.forEach(file => {
    console.log(`${file.status} - ${file.url}`);
  });

  console.log('\n=== main.js Check ===');
  const mainJsCheck = jsFiles.find(f => f.url.includes('main.js'));
  console.log(mainJsCheck ? `Found: ${mainJsCheck.url}` : 'NOT FOUND!');

  await browser.close();
})();
