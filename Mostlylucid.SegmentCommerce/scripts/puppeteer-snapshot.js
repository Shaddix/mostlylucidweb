const fs = require('fs');
const path = require('path');
const puppeteer = require('puppeteer');

(async () => {
  const baseUrl = process.env.BASE_URL || 'http://localhost:8082';
  const outDir = path.join(__dirname, 'screenshots');

  if (!fs.existsSync(outDir)) {
    fs.mkdirSync(outDir, { recursive: true });
  }

  const browser = await puppeteer.launch({ headless: 'new' });
  const page = await browser.newPage();
  await page.setViewport({ width: 1400, height: 900 });

  // Home page
  console.log('Capturing home page...');
  await page.goto(baseUrl, { waitUntil: 'networkidle2', timeout: 30000 });
  await new Promise(resolve => setTimeout(resolve, 3000)); // Wait longer for images
  
  // Wait for all images to load
  await page.evaluate(async () => {
    const images = document.querySelectorAll('img');
    await Promise.all(Array.from(images).map(img => {
      if (img.complete) return Promise.resolve();
      return new Promise((resolve) => {
        img.addEventListener('load', resolve);
        img.addEventListener('error', resolve);
        setTimeout(resolve, 5000); // Timeout after 5s per image
      });
    }));
  });
  
  await page.screenshot({ path: path.join(outDir, 'home.png'), fullPage: true });
  console.log('Saved home.png');

  // Products page (viewport only - too many products for full page)
  console.log('Capturing products page...');
  await page.goto(`${baseUrl}/products`, { waitUntil: 'networkidle2', timeout: 30000 });
  await new Promise(resolve => setTimeout(resolve, 1500));
  await page.screenshot({ path: path.join(outDir, 'products.png') });
  console.log('Saved products.png');

  // Books category - has AI-generated images
  console.log('Capturing books category (with generated images)...');
  await page.goto(`${baseUrl}/products/category/books`, { waitUntil: 'networkidle2', timeout: 30000 });
  await new Promise(resolve => setTimeout(resolve, 2000));
  await page.screenshot({ path: path.join(outDir, 'books-category.png') });
  console.log('Saved books-category.png');

  // Search for products with AI-generated images
  console.log('Capturing products with generated images...');
  await page.goto(`${baseUrl}/products/search?q=zenmat+cork`, { waitUntil: 'networkidle2', timeout: 30000 });
  await new Promise(resolve => setTimeout(resolve, 2000));
  await page.screenshot({ path: path.join(outDir, 'ai-images.png') });
  console.log('Saved ai-images.png');

  // Search results (viewport only)
  console.log('Capturing search results...');
  await page.goto(`${baseUrl}/products/search?q=wireless`, { waitUntil: 'networkidle2', timeout: 30000 });
  await new Promise(resolve => setTimeout(resolve, 1500));
  await page.screenshot({ path: path.join(outDir, 'search.png') });
  console.log('Saved search.png');

  // Test search bar interaction
  console.log('Testing search bar...');
  await page.goto(baseUrl, { waitUntil: 'networkidle2', timeout: 30000 });
  await new Promise(resolve => setTimeout(resolve, 1000));
  
  // Click on search input and type
  const searchInput = await page.$('input[type="search"]');
  if (searchInput) {
    await searchInput.click();
    await searchInput.type('laptop', { delay: 100 });
    await new Promise(resolve => setTimeout(resolve, 1500)); // Wait for results dropdown
    await page.screenshot({ path: path.join(outDir, 'search-dropdown.png') });
    console.log('Saved search-dropdown.png');
  }

  await browser.close();
  console.log('Done!');
})();
