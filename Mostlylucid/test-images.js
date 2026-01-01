const puppeteer = require('puppeteer');

(async () => {
    const browser = await puppeteer.launch({
        headless: true,
        args: ['--ignore-certificate-errors', '--no-sandbox']
    });

    const page = await browser.newPage();
    await page.setViewport({ width: 1280, height: 1024 });

    // Track broken images
    const brokenImages = [];
    const loadedImages = [];

    // Listen for failed requests
    page.on('response', response => {
        const url = response.url();
        if (url.match(/\.(png|jpg|jpeg|gif|webp|svg)/i)) {
            if (response.status() >= 400) {
                brokenImages.push({ url, status: response.status() });
            } else {
                loadedImages.push({ url, status: response.status() });
            }
        }
    });

    // Test the home page
    console.log('Loading home page...');
    await page.goto('https://localhost:7240/', {
        waitUntil: 'networkidle2',
        timeout: 60000
    });

    // Take screenshot
    await page.screenshot({ path: 'image-test-home.png', fullPage: false });

    // Now test a blog post (one that likely has images)
    console.log('\nLoading a blog post...');
    await page.goto('https://localhost:7240/blog/adding-entity-framework-core-to-an-existing-aspnet-core-project', {
        waitUntil: 'networkidle2',
        timeout: 60000
    });

    await page.screenshot({ path: 'image-test-blog.png', fullPage: false });

    // Check for images on the page
    const pageImages = await page.evaluate(() => {
        return Array.from(document.querySelectorAll('img')).map(img => ({
            src: img.src,
            complete: img.complete,
            naturalWidth: img.naturalWidth,
            alt: img.alt
        }));
    });

    console.log('\n=== Image Report ===');
    console.log('Loaded images:', loadedImages.length);
    console.log('Broken images:', brokenImages.length);

    if (brokenImages.length > 0) {
        console.log('\nBroken images:');
        brokenImages.forEach(img => console.log(`  ${img.status}: ${img.url}`));
    }

    console.log('\nPage images (from DOM):');
    pageImages.forEach(img => {
        const status = img.complete && img.naturalWidth > 0 ? 'OK' : 'BROKEN';
        console.log(`  [${status}] ${img.src.substring(0, 80)}...`);
    });

    await browser.close();
    console.log('\nDone!');
})();
