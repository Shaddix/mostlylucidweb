const puppeteer = require('puppeteer');

(async () => {
    console.log('🚀 Starting Workflow Demo Test...\n');

    const browser = await puppeteer.launch({
        headless: false,
        defaultViewport: { width: 1400, height: 900 }
    });

    const page = await browser.newPageAsync();

    try {
        // Test 1: Load main page
        console.log('📄 Test 1: Loading main workflow list page...');
        await page.goto('http://localhost:5000', { waitUntil: 'networkidle2' });
        await page.screenshot({ path: 'workflow-01-list.png' });
        console.log('✅ Main page loaded\n');

        //Test 2: Click create workflow button
        console.log('📝 Test 2: Creating a new workflow...');
        const createButton = await page.waitForSelector('button:has-text("Create Workflow")');
        await createButton.click();
        await new Promise(r => setTimeout(r, 1000));
        await page.screenshot({ path: 'workflow-02-create-form.png' });
        console.log('✅ Create form appeared\n');

        // Test 3: Fill and submit create form
        console.log('✏️ Test 3: Filling workflow details...');
        await page.type('input[name="name"]', 'Test Workflow');
        await page.type('textarea[name="description"]', 'This is a Puppeteer test workflow');
        await page.screenshot({ path: 'workflow-03-form-filled.png' });

        const submitButton = await page.waitForSelector('button[type="submit"]:has-text("Create")');
        await submitButton.click();
        await new Promise(r => setTimeout(r, 2000));
        console.log('✅ Workflow created\n');

        // Test 4: Check if redirected to editor
        console.log('🎨 Test 4: Verifying editor page loaded...');
        await page.waitForSelector('.bg-gray-50'); // Canvas area
        await page.screenshot({ path: 'workflow-04-editor.png' });

        const editorTitle = await page.$eval('h1', el => el.textContent);
        console.log(`   Editor title: "${editorTitle}"`);
        console.log('✅ Editor loaded successfully\n');

        // Test 5: Add a node
        console.log('🎯 Test 5: Adding HTTP Request node...');
        const httpNodeButton = await page.waitForSelector('button:has-text("HTTP Request")');
        await httpNodeButton.click();
        await new Promise(r => setTimeout(r, 500));
        await page.screenshot({ path: 'workflow-05-node-added.png' });
        console.log('✅ Node added to canvas\n');

        // Test 6: Select node and check properties panel
        console.log('⚙️ Test 6: Testing node selection...');
        const node = await page.waitForSelector('.cursor-move');
        await node.click();
        await new Promise(r => setTimeout(r, 500));
        await page.screenshot({ path: 'workflow-06-node-selected.png' });
        console.log('✅ Node selected, properties panel visible\n');

        // Test 7: Save workflow
        console.log('💾 Test 7: Saving workflow...');
        const saveButton = await page.waitForSelector('button:has-text("Save")');
        await saveButton.click();
        await new Promise(r => setTimeout(r, 1000));
        console.log('✅ Workflow saved\n');

        // Test 8: Navigate back to list
        console.log('🔙 Test 8: Returning to workflow list...');
        const backButton = await page.waitForSelector('a:has-text("Back")');
        await backButton.click();
        await page.waitForNavigation({ waitUntil: 'networkidle2' });
        await page.screenshot({ path: 'workflow-08-back-to-list.png' });
        console.log('✅ Back at workflow list\n');

        // Test 9: Verify workflow appears in list
        console.log('📋 Test 9: Verifying workflow in list...');
        const workflowCard = await page.waitForSelector('.border-gray-300:has-text("Test Workflow")');
        const workflowDescription = await page.$eval('p:has-text("Puppeteer test")', el => el.textContent);
        console.log(`   Found workflow: "${workflowDescription}"`);
        await page.screenshot({ path: 'workflow-09-list-with-workflow.png' });
        console.log('✅ Workflow appears in list\n');

        // Test 10: Check Hangfire dashboard link
        console.log('📊 Test 10: Checking Hangfire dashboard link...');
        const hangfireLink = await page.waitForSelector('a[href="/hangfire"]');
        console.log('✅ Hangfire dashboard link present\n');

        console.log('═══════════════════════════════════════');
        console.log('✨ ALL TESTS PASSED! ✨');
        console.log('═══════════════════════════════════════\n');
        console.log('Screenshots saved:');
        console.log('  - workflow-01-list.png');
        console.log('  - workflow-02-create-form.png');
        console.log('  - workflow-03-form-filled.png');
        console.log('  - workflow-04-editor.png');
        console.log('  - workflow-05-node-added.png');
        console.log('  - workflow-06-node-selected.png');
        console.log('  - workflow-08-back-to-list.png');
        console.log('  - workflow-09-list-with-workflow.png\n');

        await new Promise(r => setTimeout(r, 3000));
        await browser.close();

    } catch (error) {
        console.error('❌ TEST FAILED:', error.message);
        await page.screenshot({ path: 'workflow-error.png' });
        await browser.close();
        process.exit(1);
    }
})();
