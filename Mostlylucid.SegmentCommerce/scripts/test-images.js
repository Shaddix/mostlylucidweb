const puppeteer = require('puppeteer');

(async () => {
  const baseUrl = process.env.BASE_URL || 'http://localhost:8082';
  
  const browser = await puppeteer.launch({ headless: 'new' });
  const page = await browser.newPage();
  await page.setViewport({ width: 1400, height: 900 });

  // Track failed image loads
  const failedImages = [];
  const loadedImages = [];
  
  page.on('response', response => {
    const url = response.url();
    if (url.match(/\.(jpg|jpeg|png|gif|svg|webp)$/i) || url.includes('/api/placeholder/') || url.includes('/images/')) {
      if (response.status() >= 400) {
        failedImages.push({ url, status: response.status() });
      } else {
        loadedImages.push({ url, status: response.status() });
      }
    }
  });

  page.on('requestfailed', request => {
    const url = request.url();
    if (url.match(/\.(jpg|jpeg|png|gif|svg|webp)$/i) || url.includes('/api/placeholder/') || url.includes('/images/')) {
      failedImages.push({ url, error: request.failure().errorText });
    }
  });

  console.log('Loading home page...');
  await page.goto(baseUrl, { waitUntil: 'networkidle2', timeout: 30000 });
  await new Promise(resolve => setTimeout(resolve, 3000));

  // Get all image elements and their src
  const imageSrcs = await page.evaluate(() => {
    const imgs = document.querySelectorAll('img');
    return Array.from(imgs).map(img => ({
      src: img.src,
      naturalWidth: img.naturalWidth,
      naturalHeight: img.naturalHeight,
      complete: img.complete,
      alt: img.alt
    }));
  });

  console.log('\n=== IMAGE ANALYSIS ===\n');
  
  console.log('Images found on page:');
  imageSrcs.forEach((img, i) => {
    const loaded = img.complete && img.naturalWidth > 0;
    console.log(`  ${i + 1}. ${loaded ? '✓' : '✗'} ${img.src.substring(0, 80)}...`);
    console.log(`     Size: ${img.naturalWidth}x${img.naturalHeight}, Complete: ${img.complete}`);
  });

  console.log('\n\nNetwork - Loaded images:');
  loadedImages.forEach(img => {
    console.log(`  ✓ [${img.status}] ${img.url.substring(0, 100)}`);
  });

  console.log('\n\nNetwork - Failed images:');
  if (failedImages.length === 0) {
    console.log('  None!');
  } else {
    failedImages.forEach(img => {
      console.log(`  ✗ [${img.status || img.error}] ${img.url}`);
    });
  }

  // Test a specific placeholder URL
  console.log('\n\n=== TESTING PLACEHOLDER API ===\n');
  const testUrl = `${baseUrl}/api/placeholder/tech/TestProduct?w=200&h=200`;
  const response = await page.goto(testUrl, { waitUntil: 'networkidle0' });
  console.log(`Placeholder API test: ${response.status()} ${response.headers()['content-type']}`);

  await browser.close();
  
  if (failedImages.length > 0) {
    console.log('\n⚠️  Some images failed to load!');
    process.exit(1);
  } else {
    console.log('\n✓ All images loaded successfully!');
  }
})();
