const puppeteer = require('puppeteer');

async function testBlogPagination() {
    console.log('Starting Puppeteer test for blog list pagination...\n');

    const browser = await puppeteer.launch({
        headless: false,
        defaultViewport: { width: 1400, height: 1200 }
    });

    const page = await browser.newPage();

    try {
        // Navigate to the blog list page with small page size to see pagination
        console.log('Navigating to blog list page with pageSize=3...');
        await page.goto('http://localhost:8080/blog?page=1&pageSize=3', {
            waitUntil: 'networkidle2',
            timeout: 30000
        });

        console.log('Page loaded successfully!');

        // Wait a moment for any dynamic content
        await new Promise(r => setTimeout(r, 2000));

        // Take full page screenshot
        const screenshotPath = 'C:\\Blog\\mostlylucidweb\\Mostlylucid\\blog-pagination-full.png';
        await page.screenshot({
            path: screenshotPath,
            fullPage: true
        });
        console.log(`✅ Full page screenshot saved to: ${screenshotPath}`);

        // Get page dimensions and pagination positions
        const pageInfo = await page.evaluate(() => {
            const topPagination = document.querySelector('.border.border-gray-200.dark\\:border-gray-700 paging');
            const bottomPagination = document.querySelectorAll('paging')[1]; // Second paging element
            const content = document.getElementById('content');

            return {
                pageHeight: document.documentElement.scrollHeight,
                viewportHeight: window.innerHeight,
                contentHeight: content ? content.offsetHeight : 0,
                topPaginationPosition: topPagination ? topPagination.getBoundingClientRect().top : null,
                bottomPaginationPosition: bottomPagination ? bottomPagination.getBoundingClientRect().top : null,
                topPaginationExists: !!topPagination,
                bottomPaginationExists: !!bottomPagination,
                postCount: document.querySelectorAll('.mb-5').length, // Assuming posts have mb-5 class
            };
        });

        console.log('\n📊 Page Analysis:');
        console.log(`  Page height: ${pageInfo.pageHeight}px`);
        console.log(`  Viewport height: ${pageInfo.viewportHeight}px`);
        console.log(`  Content height: ${pageInfo.contentHeight}px`);
        console.log(`  Top pagination exists: ${pageInfo.topPaginationExists}`);
        console.log(`  Bottom pagination exists: ${pageInfo.bottomPaginationExists}`);
        console.log(`  Post count: ${pageInfo.postCount}`);

        // Scroll to bottom to capture bottom pagination
        await page.evaluate(() => {
            window.scrollTo(0, document.body.scrollHeight);
        });
        await new Promise(r => setTimeout(r, 1000));

        const bottomScreenshot = 'C:\\Blog\\mostlylucidweb\\Mostlylucid\\blog-pagination-bottom.png';
        await page.screenshot({
            path: bottomScreenshot,
            fullPage: false
        });
        console.log(`✅ Bottom pagination screenshot saved to: ${bottomScreenshot}`);

        // Scroll to top pagination
        await page.evaluate(() => {
            window.scrollTo(0, 0);
        });
        await new Promise(r => setTimeout(r, 1000));

        const topScreenshot = 'C:\\Blog\\mostlylucidweb\\Mostlylucid\\blog-pagination-top.png';
        await page.screenshot({
            path: topScreenshot,
            fullPage: false
        });
        console.log(`✅ Top pagination screenshot saved to: ${topScreenshot}`);

        // Test mobile view
        console.log('\n📱 Testing mobile view...');
        await page.setViewport({ width: 375, height: 667 }); // iPhone SE size
        await new Promise(r => setTimeout(r, 1000));

        const mobileScreenshot = 'C:\\Blog\\mostlylucidweb\\Mostlylucid\\blog-pagination-mobile.png';
        await page.screenshot({
            path: mobileScreenshot,
            fullPage: true
        });
        console.log(`✅ Mobile screenshot saved to: ${mobileScreenshot}`);

        // Test tablet view
        console.log('\n📱 Testing tablet view...');
        await page.setViewport({ width: 768, height: 1024 }); // iPad size
        await new Promise(r => setTimeout(r, 1000));

        const tabletScreenshot = 'C:\\Blog\\mostlylucidweb\\Mostlylucid\\blog-pagination-tablet.png';
        await page.screenshot({
            path: tabletScreenshot,
            fullPage: true
        });
        console.log(`✅ Tablet screenshot saved to: ${tabletScreenshot}`);

        console.log('\n⏸️  Browser will stay open for 10 seconds for inspection...');
        await new Promise(resolve => setTimeout(resolve, 10000));

    } catch (error) {
        console.error('❌ Error:', error.message);

        // Take error screenshot
        const errorScreenshot = 'C:\\Blog\\mostlylucidweb\\Mostlylucid\\blog-pagination-error.png';
        await page.screenshot({
            path: errorScreenshot,
            fullPage: true
        });
        console.log(`Error screenshot saved to: ${errorScreenshot}`);
    }

    await browser.close();
    console.log('\n✅ Test complete!');
}

testBlogPagination().catch(console.error);
