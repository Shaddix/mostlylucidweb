const puppeteer = require('puppeteer');

(async () => {
    const browser = await puppeteer.launch({
        headless: true,
        args: ['--ignore-certificate-errors', '--no-sandbox']
    });

    const page = await browser.newPage();
    await page.setViewport({ width: 1280, height: 1024 });

    // Test Software page in LIGHT mode
    console.log('Loading Software page in light mode...');
    await page.goto('https://localhost:7240/software', {
        waitUntil: 'networkidle2',
        timeout: 30000
    });

    // Force light mode by removing dark class if present
    await page.evaluate(() => {
        document.body.classList.remove('dark');
        document.documentElement.classList.remove('dark');
        // Set Alpine.js state if available
        if (window.Alpine) {
            document.body.__x && document.body.__x.$data && (document.body.__x.$data.isDarkMode = false);
        }
        // Also try localStorage
        localStorage.setItem('theme', 'light');
    });

    // Wait for styles to apply
    await new Promise(r => setTimeout(r, 1000));

    // Take screenshot
    await page.screenshot({
        path: 'software-light-mode.png',
        fullPage: true
    });
    console.log('Screenshot saved: software-light-mode.png');

    // Now test dark mode
    console.log('Testing dark mode...');
    await page.evaluate(() => {
        document.body.classList.add('dark');
        document.documentElement.classList.add('dark');
        localStorage.setItem('theme', 'dark');
    });

    await new Promise(r => setTimeout(r, 1000));

    await page.screenshot({
        path: 'software-dark-mode.png',
        fullPage: true
    });
    console.log('Screenshot saved: software-dark-mode.png');

    await browser.close();
    console.log('Done!');
})();
