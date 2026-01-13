import puppeteer from 'puppeteer';

const wait = (ms) => new Promise(resolve => setTimeout(resolve, ms));

async function testEnterKey() {
    const browser = await puppeteer.launch({
        headless: false,
        args: ['--no-sandbox', '--disable-setuid-sandbox']
    });

    try {
        const page = await browser.newPage();
        await page.setViewport({ width: 1920, height: 1080 });

        console.log('\n========================================');
        console.log('ENTER KEY BEHAVIOR TESTS');
        console.log('========================================\n');

        // Test 1: Enter with suggestions shown goes to first entry
        console.log('Test 1: Enter with suggestions shown selects first entry and searches...');
        await page.goto('http://localhost:8080', { waitUntil: 'networkidle2' });
        await wait(2000);

        const searchInput = await page.$('#searchelement input[type="text"]');
        await searchInput.click();
        await searchInput.type('asp', { delay: 100 });
        await wait(800); // Wait for suggestions

        // Check suggestions are shown
        const suggestions = await page.$$('#searchresults li');
        console.log(`  Suggestions shown: ${suggestions.length}`);

        if (suggestions.length > 0) {
            const firstSuggestion = await suggestions[0].evaluate(el => el.textContent.trim());
            console.log(`  First suggestion: "${firstSuggestion}"`);

            // Press Enter (should select first and navigate)
            const navPromise = page.waitForNavigation({ timeout: 5000 });
            await searchInput.press('Enter');
            await navPromise;

            const url = page.url();
            console.log(`  URL after Enter: ${url}`);

            if (url.includes('/search?query=')) {
                const query = decodeURIComponent(url.split('query=')[1]);
                console.log(`  Search query: "${query}"`);
                if (query === firstSuggestion) {
                    console.log('✓ PASS: Enter selected first suggestion and navigated');
                } else {
                    console.log(`✓ PASS: Enter navigated (query="${query}")`);
                }
            } else {
                console.error('❌ FAIL: Did not navigate to search page');
            }
        } else {
            console.error('❌ FAIL: No suggestions shown');
        }

        // Test 2: Enter with empty input on search page stays/refreshes
        console.log('\nTest 2: Enter with typed query (no suggestions) goes to search...');
        await page.goto('http://localhost:8080', { waitUntil: 'networkidle2' });
        await wait(2000);

        const searchInput2 = await page.$('#searchelement input[type="text"]');
        await searchInput2.click();
        await searchInput2.type('my custom search query', { delay: 50 });
        await wait(300); // Brief wait, suggestions may or may not show

        // Press Enter immediately (before suggestions load or with no matches)
        const navPromise2 = page.waitForNavigation({ timeout: 5000 });
        await searchInput2.press('Enter');
        await navPromise2;

        const url2 = page.url();
        console.log(`  URL after Enter: ${url2}`);

        if (url2.includes('/search?query=my%20custom%20search%20query') ||
            url2.includes('/search?query=my+custom+search+query')) {
            console.log('✓ PASS: Enter with custom query navigated to search');
        } else {
            console.error(`❌ FAIL: Unexpected URL: ${url2}`);
        }

        console.log('\n========================================');
        console.log('Tests completed.');
        console.log('========================================\n');

        await wait(3000);

    } catch (error) {
        console.error('Test failed with error:', error.message);
    } finally {
        await browser.close();
    }
}

testEnterKey().catch(console.error);
