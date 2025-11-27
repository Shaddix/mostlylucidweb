const puppeteer = require('puppeteer');
const { marked } = require('marked');

async function testMarkdownRendering() {
    console.log('Starting Puppeteer test for markdown rendering...\n');

    const browser = await puppeteer.launch({
        headless: false, // Set to false to see the browser
        defaultViewport: { width: 1200, height: 800 }
    });

    const page = await browser.newPage();

    // Test cases showing the issue
    const testCases = [
        {
            title: 'Original English',
            markdown: '**Test. New Text **',
            description: 'Original markdown with trailing space in bold'
        },
        {
            title: 'Broken Translation Output (Current)',
            markdown: '**Esto es una prueba',
            description: 'Missing closing ** due to Markdig reconstruction issue'
        },
        {
            title: 'Expected Translation Output',
            markdown: '**Esto es una prueba **',
            description: 'What it should look like with proper ** closure'
        },
        {
            title: 'Test with emoji and symbols',
            markdown: '**Hello 👋 World! **',
            description: 'Testing emoji preservation'
        },
        {
            title: 'Multiple spaces',
            markdown: '**Text.  Two spaces **',
            description: 'Testing multiple space preservation'
        }
    ];

    // Create HTML with all test cases
    const html = `
<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>Markdown Rendering Test</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            max-width: 1200px;
            margin: 40px auto;
            padding: 20px;
            background: #f5f5f5;
        }
        h1 {
            color: #333;
            border-bottom: 3px solid #007acc;
            padding-bottom: 10px;
        }
        .test-case {
            background: white;
            padding: 20px;
            margin: 20px 0;
            border-radius: 8px;
            box-shadow: 0 2px 4px rgba(0,0,0,0.1);
        }
        .test-title {
            font-size: 18px;
            font-weight: bold;
            color: #007acc;
            margin-bottom: 5px;
        }
        .description {
            color: #666;
            font-size: 14px;
            margin-bottom: 15px;
            font-style: italic;
        }
        .markdown-source {
            background: #f0f0f0;
            border: 1px solid #ddd;
            padding: 10px;
            font-family: 'Consolas', 'Monaco', monospace;
            font-size: 13px;
            border-radius: 4px;
            margin-bottom: 10px;
            white-space: pre-wrap;
        }
        .rendered {
            border: 2px solid #007acc;
            padding: 15px;
            min-height: 40px;
            background: #fafafa;
            border-radius: 4px;
        }
        .label {
            font-weight: bold;
            color: #333;
            margin-top: 10px;
            margin-bottom: 5px;
        }
        strong {
            color: #d73a49;
            font-weight: bold;
        }
        .issue {
            background: #fff3cd;
            border-left: 4px solid #ffc107;
            padding: 10px;
            margin-top: 10px;
        }
    </style>
</head>
<body>
    <h1>🔍 Markdown Translation Rendering Test</h1>
    <p style="color: #666; font-size: 16px;">
        This page shows how different markdown variations render in the browser.
        The issue is that the closing ** is missing when markdown is reconstructed after translation.
    </p>

    ${testCases.map((test, index) => `
        <div class="test-case">
            <div class="test-title">Test ${index + 1}: ${test.title}</div>
            <div class="description">${test.description}</div>

            <div class="label">Markdown Source:</div>
            <div class="markdown-source">${escapeHtml(test.markdown)}</div>

            <div class="label">Rendered Output:</div>
            <div class="rendered">${marked.parse(test.markdown)}</div>

            ${index === 1 ? `
                <div class="issue">
                    ⚠️ <strong>Issue:</strong> The bold formatting is not closed properly.
                    Notice how "prueba" appears bold but the ** marker is missing, causing
                    everything after it to potentially be bold.
                </div>
            ` : ''}
        </div>
    `).join('')}

    <div class="test-case" style="background: #e8f5e9;">
        <div class="test-title">✅ Summary</div>
        <div class="description">
            <p><strong>Root Cause:</strong> The translation service removes trailing spaces, and Markdig's ToMarkdownString()
            doesn't properly reconstruct the closing ** when the content changes.</p>
            <p><strong>Fixes Applied:</strong></p>
            <ul>
                <li>✓ Removed sentence splitting that was losing spaces</li>
                <li>✓ Added whitespace preservation logic</li>
                <li>✓ Removed emoji filtering</li>
            </ul>
            <p><strong>Still Needed:</strong></p>
            <ul>
                <li>⚠️ Translation service should preserve leading/trailing whitespace</li>
                <li>⚠️ Or use a different markdown reconstruction method</li>
            </ul>
        </div>
    </div>
</body>
</html>
`;

    function escapeHtml(text) {
        return text
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;')
            .replace(/ /g, '·'); // Show spaces as middle dots
    }

    await page.setContent(html);

    // Take a screenshot
    const screenshotPath = 'C:\\Blog\\mostlylucidweb\\Mostlylucid\\markdown-rendering-test.png';
    await page.screenshot({
        path: screenshotPath,
        fullPage: true
    });

    console.log(`✅ Screenshot saved to: ${screenshotPath}`);
    console.log('\nTest cases rendered:');
    testCases.forEach((test, i) => {
        console.log(`  ${i + 1}. ${test.title}`);
        console.log(`     Markdown: "${test.markdown}"`);
    });

    console.log('\n⏸️  Browser will stay open for 10 seconds so you can inspect...');
    await new Promise(resolve => setTimeout(resolve, 10000));

    await browser.close();
    console.log('\n✅ Test complete!');
}

testMarkdownRendering().catch(console.error);
