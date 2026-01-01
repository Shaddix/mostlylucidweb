const puppeteer = require('puppeteer');

(async () => {
    const browser = await puppeteer.launch({
        headless: true,
        args: ['--ignore-certificate-errors', '--no-sandbox']
    });

    const page = await browser.newPage();
    await page.setViewport({ width: 1280, height: 1024 });

    // Test GraphRAG page with math formulas
    console.log('Loading GraphRAG page with math formulas...');
    await page.goto('https://localhost:7240/blog/graphrag-minimum-viable-implementation', {
        waitUntil: 'networkidle2',
        timeout: 60000
    });

    // Wait for any KaTeX rendering
    await new Promise(r => setTimeout(r, 2000));

    // Scroll to where the math formulas are (around the IDF section)
    await page.evaluate(() => {
        // Look for the IDF heading and scroll there
        const idfHeading = Array.from(document.querySelectorAll('h2, h3, strong'))
            .find(el => el.textContent.includes('IDF'));
        if (idfHeading) {
            idfHeading.scrollIntoView({ block: 'start' });
        }
    });

    await new Promise(r => setTimeout(r, 500));

    // Take screenshot
    await page.screenshot({
        path: 'math-test.png',
        fullPage: false  // Just visible area
    });
    console.log('Screenshot saved: math-test.png');

    // Check if any katex elements exist
    const katexCount = await page.evaluate(() => {
        return document.querySelectorAll('.katex, .katex-display-wrapper, .katex-inline-wrapper').length;
    });
    console.log('KaTeX elements found:', katexCount);

    // Check for raw LaTeX text (should NOT be present if KaTeX is working)
    const rawLatex = await page.evaluate(() => {
        return document.body.innerText.includes('\\frac') ||
               document.body.innerText.includes('\\sum');
    });
    console.log('Raw LaTeX visible (should be false):', rawLatex);

    await browser.close();
    console.log('Done!');
})();
