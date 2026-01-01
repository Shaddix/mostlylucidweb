import katex from 'katex';
import 'katex/dist/katex.min.css';

/**
 * Renders LaTeX math expressions in the given container.
 * Supports:
 * - Display math: $$ ... $$ or \[ ... \]
 * - Inline math: $ ... $ (single dollar signs with no spaces after opening/before closing)
 *
 * @param {Element} container - The container element to search for math expressions
 */
export function renderMath(container = document) {
    // Get all text nodes that might contain math
    const blogContent = container.querySelectorAll('.prose, .blog-content, article, [data-render-math]');

    if (blogContent.length === 0) {
        // Fallback to looking for specific elements with math content
        const mainContent = container.querySelector('#contentcontainer') || container.querySelector('main') || container;
        renderMathInElement(mainContent);
        return;
    }

    blogContent.forEach(element => {
        renderMathInElement(element);
    });
}

function renderMathInElement(element) {
    // Skip if already processed
    if (element.hasAttribute('data-math-rendered')) {
        return;
    }

    // Skip code blocks and pre elements
    if (element.tagName === 'CODE' || element.tagName === 'PRE') {
        return;
    }

    const walker = document.createTreeWalker(
        element,
        NodeFilter.SHOW_TEXT,
        {
            acceptNode: function(node) {
                // Skip text inside code, pre, script, style elements
                const parent = node.parentElement;
                if (parent && ['CODE', 'PRE', 'SCRIPT', 'STYLE', 'TEXTAREA', 'KBD'].includes(parent.tagName)) {
                    return NodeFilter.FILTER_REJECT;
                }
                // Skip if already in a katex element
                if (parent && parent.closest('.katex')) {
                    return NodeFilter.FILTER_REJECT;
                }
                return NodeFilter.FILTER_ACCEPT;
            }
        }
    );

    const textNodes = [];
    while (walker.nextNode()) {
        textNodes.push(walker.currentNode);
    }

    // Process text nodes in reverse to avoid index issues when modifying DOM
    for (let i = textNodes.length - 1; i >= 0; i--) {
        const textNode = textNodes[i];
        processTextNode(textNode);
    }

    element.setAttribute('data-math-rendered', 'true');
}

function processTextNode(textNode) {
    const text = textNode.textContent;

    // Check if there's any potential math content
    if (!text.includes('$') && !text.includes('\\[') && !text.includes('\\(')) {
        return;
    }

    const fragment = document.createDocumentFragment();
    let lastIndex = 0;
    let hasMatch = false;

    // Regex patterns for different math modes
    // Display math: $$ ... $$ or \[ ... \]
    // Inline math: $ ... $ (single $ with no space after opening or before closing)
    const patterns = [
        { regex: /\$\$([\s\S]+?)\$\$/g, display: true },
        { regex: /\\\[([\s\S]+?)\\\]/g, display: true },
        { regex: /\\\(([\s\S]+?)\\\)/g, display: false },
        { regex: /\$([^\s$][^$]*?[^\s$])\$/g, display: false }, // Single $ with no leading/trailing space
        { regex: /\$([^\s$])\$/g, display: false } // Single character between $
    ];

    // Combine all matches with their positions
    const matches = [];
    for (const { regex, display } of patterns) {
        let match;
        const re = new RegExp(regex.source, regex.flags);
        while ((match = re.exec(text)) !== null) {
            matches.push({
                start: match.index,
                end: match.index + match[0].length,
                latex: match[1],
                display: display,
                fullMatch: match[0]
            });
        }
    }

    // Sort matches by start position
    matches.sort((a, b) => a.start - b.start);

    // Remove overlapping matches (keep first one)
    const filteredMatches = [];
    let lastEnd = -1;
    for (const match of matches) {
        if (match.start >= lastEnd) {
            filteredMatches.push(match);
            lastEnd = match.end;
        }
    }

    // Process matches
    for (const match of filteredMatches) {
        // Add text before this match
        if (match.start > lastIndex) {
            fragment.appendChild(document.createTextNode(text.slice(lastIndex, match.start)));
        }

        // Render the LaTeX
        try {
            const span = document.createElement('span');
            span.className = match.display ? 'katex-display-wrapper' : 'katex-inline-wrapper';

            katex.render(match.latex, span, {
                displayMode: match.display,
                throwOnError: false,
                errorColor: '#cc0000',
                strict: false,
                trust: true,
                macros: {
                    // Common macros
                    "\\R": "\\mathbb{R}",
                    "\\N": "\\mathbb{N}",
                    "\\Z": "\\mathbb{Z}",
                    "\\C": "\\mathbb{C}",
                    "\\Q": "\\mathbb{Q}"
                }
            });

            fragment.appendChild(span);
            hasMatch = true;
        } catch (err) {
            console.warn('KaTeX rendering failed for:', match.latex, err);
            // Fall back to original text
            fragment.appendChild(document.createTextNode(match.fullMatch));
        }

        lastIndex = match.end;
    }

    // Add remaining text
    if (lastIndex < text.length) {
        fragment.appendChild(document.createTextNode(text.slice(lastIndex)));
    }

    // Only replace if we found matches
    if (hasMatch && fragment.childNodes.length > 0) {
        textNode.parentNode.replaceChild(fragment, textNode);
    }
}

// Export for use in main.js
export default renderMath;
