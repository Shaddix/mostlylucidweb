import puppeteer from 'puppeteer';

const wait = (ms) => new Promise(resolve => setTimeout(resolve, ms));

async function testSearch() {
    const browser = await puppeteer.launch({
        headless: false,
        args: ['--no-sandbox', '--disable-setuid-sandbox']
    });

    try {
        const page = await browser.newPage();

        // Set viewport to large screen so typeahead is visible (it's hidden on small screens)
        await page.setViewport({ width: 1920, height: 1080 });

        // Enable console logging from the page
        page.on('console', msg => console.log('PAGE LOG:', msg.text()));
        page.on('pageerror', error => console.log('PAGE ERROR:', error.message));

        await page.goto('http://localhost:8080', { waitUntil: 'networkidle2' });

        // Wait for Alpine to fully initialize components
        await wait(3000);

        console.log('\n========================================');
        console.log('TYPEAHEAD AUTOCOMPLETE TESTS');
        console.log('========================================\n');

        // Test 1: Typeahead shows suggestions
        console.log('Test 1: Typeahead shows suggestions for "asp"...');

        // Check if typeahead function is available
        const typeaheadTest = await page.evaluate(() => {
            const available = typeof window.mostlylucid !== 'undefined' && typeof window.mostlylucid.typeahead === 'function';

            // Try calling it
            let callResult = null;
            let callError = null;
            if (available) {
                try {
                    callResult = window.mostlylucid.typeahead();
                    console.log('[TEST] Manual call result:', callResult);
                } catch (e) {
                    callError = e.message;
                }
            }

            return { available, callWorks: callResult !== null, callError };
        });
        console.log(`  typeahead function test:`, JSON.stringify(typeaheadTest));

        // Check if Alpine is initialized on the searchelement
        const alpineData = await page.evaluate(() => {
            const el = document.getElementById('searchelement');
            if (!el) return { found: false, error: 'Element not found' };

            // Check all Alpine-related properties
            const xData = el.getAttribute('x-data');
            const hasX = !!el.__x;
            const hasXData = !!el.__x?.$data;

            // Check if Alpine is tracking this element
            let alpineStoreHasIt = false;
            try {
                // Alpine stores components internally, check if searchelement is known
                if (window.Alpine && window.Alpine.$data) {
                    alpineStoreHasIt = !!window.Alpine.$data(el);
                }
            } catch (e) {}

            // Try to get data using Alpine's public API
            let publicData = null;
            try {
                if (window.Alpine && typeof window.Alpine.$data === 'function') {
                    publicData = window.Alpine.$data(el);
                }
            } catch (e) {}

            return {
                found: true,
                hasXDataAttr: !!xData,
                xDataValue: xData,
                hasX: hasX,
                hasXData: hasXData,
                alpineStoreHasIt,
                hasPublicData: !!publicData,
                publicDataQuery: publicData?.query,
                xProperties: el.__x ? Object.keys(el.__x) : []
            };
        });
        console.log('  Alpine data on searchelement:', JSON.stringify(alpineData, null, 2));

        const searchInput = await page.$('#searchelement input[type="text"]');
        if (!searchInput) {
            console.error('❌ FAIL: Search input not found');
            return;
        }

        // Click to focus the input first
        await searchInput.click();
        await wait(200);

        // Clear any existing value
        await searchInput.evaluate(el => el.value = '');

        // Type using Puppeteer
        await searchInput.type('asp', { delay: 150 });
        await wait(1500); // Wait longer for debounce + API call + rendering

        // Check if query was updated
        const queryValue = await page.evaluate(() => {
            const el = document.getElementById('searchelement');
            const data = window.Alpine?.$data(el);
            return {
                inputValue: document.querySelector('#searchelement input').value,
                alpineQuery: data?.query,
                resultsLength: data?.results?.length
            };
        });
        console.log(`  Query state:`, JSON.stringify(queryValue));

        // Debug: Check if results container exists
        const resultsContainer = await page.$('#searchresults');
        console.log(`  Results container exists: ${resultsContainer !== null}`);

        const suggestions = await page.$$('#searchresults li');
        if (suggestions.length > 0) {
            console.log(`✓ PASS: ${suggestions.length} suggestions shown`);

            // Print the suggestions (trimmed)
            for (let i = 0; i < suggestions.length; i++) {
                const text = await suggestions[i].evaluate(el => el.textContent.trim());
                console.log(`  - ${text}`);
            }
        } else {
            console.error('❌ FAIL: No suggestions shown');
        }

        // Test 2: Click on a suggestion navigates to search page
        console.log('\nTest 2: Clicking suggestion navigates to search page...');
        const currentUrl = page.url();
        console.log(`  Current URL: ${currentUrl}`);

        if (suggestions.length > 0) {
            // Click the first suggestion
            const firstSuggestion = suggestions[0];
            const suggestionText = await firstSuggestion.evaluate(el => el.textContent.trim());
            console.log(`  Clicking on: "${suggestionText}"`);

            await firstSuggestion.click();

            // Wait for navigation
            await page.waitForNavigation({ timeout: 5000 }).catch(() => {
                console.log('  Navigation timeout - checking URL manually...');
            });

            await wait(1000);

            const newUrl = page.url();
            console.log(`  New URL: ${newUrl}`);

            if (newUrl.includes('/search?query=')) {
                console.log('✓ PASS: Navigated to search page');

                // Check that results are displayed
                const searchResults = await page.$('#content');
                if (searchResults) {
                    console.log('✓ PASS: Search results container exists');
                } else {
                    console.error('❌ FAIL: Search results container not found');
                }
            } else {
                console.error('❌ FAIL: Did not navigate to search page');
            }
        }

        // Test 3: Arrow keys navigation
        console.log('\nTest 3: Testing arrow key navigation...');
        await page.goto('http://localhost:8080', { waitUntil: 'networkidle2' });
        const searchInput2 = await page.$('#searchelement input[type="text"]');

        await searchInput2.click();
        await searchInput2.type('htmx', { delay: 100 });
        await wait(500);

        const suggestions2 = await page.$$('#searchresults li');
        if (suggestions2.length > 0) {
            console.log(`  ${suggestions2.length} suggestions shown`);

            // Press arrow down
            await searchInput2.press('ArrowDown');
            await wait(200);

            // Check if first item is highlighted
            const highlightedClass = await suggestions2[0].evaluate(el => el.className);
            if (highlightedClass.includes('bg-blue-light') || highlightedClass.includes('bg-blue-dark')) {
                console.log('✓ PASS: Arrow down highlights first suggestion');
            } else {
                console.error('❌ FAIL: First suggestion not highlighted');
                console.error(`  Classes: ${highlightedClass}`);
            }

            // Press Enter to select and navigate
            const navigationPromise = page.waitForNavigation({ timeout: 5000 }).catch(() => null);
            await searchInput2.press('Enter');
            await navigationPromise;
            await wait(500);

            const urlAfterEnter = page.url();
            if (urlAfterEnter.includes('/search?query=')) {
                console.log('✓ PASS: Enter key navigates to search');
            } else {
                console.error('❌ FAIL: Enter key did not navigate');
                console.error(`  URL after Enter: ${urlAfterEnter}`);
            }
        }

        // Test 4: Tab key completion
        console.log('\nTest 4: Testing Tab key completion...');
        await page.goto('http://localhost:8080', { waitUntil: 'networkidle2' });
        const searchInput3 = await page.$('#searchelement input[type="text"]');

        await searchInput3.click();
        await searchInput3.type('mer', { delay: 100 });
        await wait(500);

        const suggestions3 = await page.$$('#searchresults li');
        if (suggestions3.length > 0) {
            const firstSuggestionText = await suggestions3[0].evaluate(el => el.textContent.trim());
            console.log(`  First suggestion: "${firstSuggestionText}"`);

            // Press Tab
            await searchInput3.press('Tab');
            await wait(200);

            // Check input value
            const inputValue = await searchInput3.evaluate(el => el.value.trim());
            if (inputValue === firstSuggestionText) {
                console.log(`✓ PASS: Tab completed to "${inputValue}"`);
            } else {
                console.error(`❌ FAIL: Expected "${firstSuggestionText}", got "${inputValue}"`);
            }
        }

        console.log('\n========================================');
        console.log('Tests completed. Browser will stay open for 10 seconds.');
        console.log('========================================');

        await wait(10000);

    } catch (error) {
        console.error('Test failed with error:', error);
    } finally {
        await browser.close();
    }
}

testSearch().catch(console.error);
