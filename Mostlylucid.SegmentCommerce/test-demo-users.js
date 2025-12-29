const puppeteer = require('puppeteer');
const fs = require('fs');

const delay = ms => new Promise(resolve => setTimeout(resolve, ms));

async function testDemoUsers() {
    console.log('Starting Puppeteer test...');
    
    const browser = await puppeteer.launch({ 
        headless: true,
        args: ['--window-size=1400,900']
    });
    
    const page = await browser.newPage();
    await page.setViewport({ width: 1400, height: 900 });
    
    const logs = [];
    const allRequests = [];
    
    page.on('console', msg => {
        logs.push(`[${msg.type()}] ${msg.text()}`);
    });
    
    page.on('request', req => {
        allRequests.push({ method: req.method(), url: req.url() });
    });
    
    page.on('response', res => {
        const req = allRequests.find(r => r.url === res.url());
        if (req) {
            req.status = res.status();
        }
    });

    try {
        console.log('\n=== Step 1: Load homepage ===');
        await page.goto('http://localhost:8082', { waitUntil: 'networkidle2', timeout: 15000 });
        
        if (!fs.existsSync('test-screenshots')) fs.mkdirSync('test-screenshots');
        
        console.log('\n=== Step 2: Click tracking dropdown ===');
        await page.evaluate(() => {
            const buttons = document.querySelectorAll('button');
            for (const btn of buttons) {
                if (btn.textContent?.includes('Fingerprint')) {
                    btn.click();
                    return;
                }
            }
        });
        await delay(500);
        
        console.log('\n=== Step 3: Wait for demo users ===');
        await delay(2000);
        await page.screenshot({ path: 'test-screenshots/02-dropdown-open.png' });
        
        // Clear request log before click
        const preClickRequests = allRequests.length;
        
        console.log('\n=== Step 4: Click Alex ===');
        const clicked = await page.evaluate(() => {
            const buttons = document.querySelectorAll('button');
            for (const btn of buttons) {
                const onClick = btn.getAttribute('@click') || '';
                if (onClick.includes('tech-enthusiast')) {
                    btn.click();
                    return true;
                }
            }
            return false;
        });
        console.log('Alex clicked:', clicked);
        
        // Wait for navigation to complete
        console.log('Waiting for navigation...');
        try {
            await page.waitForNavigation({ waitUntil: 'networkidle2', timeout: 10000 });
        } catch (e) {
            console.log('Navigation wait timed out or failed:', e.message);
        }
        
        // Additional wait for any HTMX requests
        await delay(2000);
        
        console.log('\n=== Step 5: Check final state ===');
        console.log('Current URL (from page.url()):', page.url());
        
        // Also check URL from JavaScript
        const jsUrl = await page.evaluate(() => window.location.href);
        console.log('Current URL (from JS):', jsUrl);
        
        // Check if we're on the homepage
        const pageTitle = await page.title();
        console.log('Page title:', pageTitle);
        
        // Check if recommendations loaded
        const hasRecommendations = await page.evaluate(() => {
            return document.querySelector('.product-card') !== null;
        });
        console.log('Has product cards:', hasRecommendations);
        
        await page.screenshot({ path: 'test-screenshots/03-final.png', fullPage: true });
        
        // Show requests made after the click
        console.log('\n=== Requests after click ===');
        const postClickRequests = allRequests.slice(preClickRequests);
        postClickRequests.forEach(r => {
            console.log(`  ${r.method} ${r.status || '???'} ${r.url}`);
        });
        
        // Show console logs
        console.log('\n=== Console logs ===');
        logs.filter(l => l.includes('SessionStore') || l.includes('TrackingManager') || l.includes('htmx') || l.includes('error'))
            .slice(-20)
            .forEach(l => console.log('  ', l));
        
        console.log('\nTest complete!');
        
    } catch (error) {
        console.error('Test error:', error.message);
    } finally {
        await browser.close();
    }
}

testDemoUsers().catch(console.error);
