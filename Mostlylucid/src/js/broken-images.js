/**
 * Broken Image Handler Module
 * Detects broken images and replaces them with a placeholder SVG
 */

// Inline SVG placeholder for broken images
const BROKEN_IMAGE_SVG = `
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" class="broken-image-placeholder">
  <rect x="3" y="3" width="18" height="18" rx="2" ry="2"/>
  <circle cx="8.5" cy="8.5" r="1.5"/>
  <polyline points="21 15 16 10 5 21"/>
  <line x1="3" y1="3" x2="21" y2="21"/>
</svg>`;

/**
 * Create a placeholder element for broken images
 * @param {HTMLImageElement} img - The broken image element
 * @returns {HTMLElement} - Placeholder element
 */
function createPlaceholder(img) {
    const placeholder = document.createElement('div');
    placeholder.className = 'broken-image-container';
    placeholder.innerHTML = BROKEN_IMAGE_SVG;

    // Preserve alt text as tooltip
    if (img.alt) {
        placeholder.title = `Image unavailable: ${img.alt}`;
    } else {
        placeholder.title = 'Image unavailable';
    }

    // Copy relevant attributes
    if (img.id) placeholder.id = img.id;
    if (img.className) placeholder.className += ' ' + img.className;

    return placeholder;
}

/**
 * Handle a single broken image
 * @param {HTMLImageElement} img - The broken image element
 */
function handleBrokenImage(img) {
    // Skip if already processed
    if (img.hasAttribute('data-broken-handled')) return;
    img.setAttribute('data-broken-handled', 'true');

    const placeholder = createPlaceholder(img);
    img.parentNode.replaceChild(placeholder, img);
}

/**
 * Set up error handlers on all images in a container
 * @param {Element} container - DOM element to search for images
 */
function setupImageErrorHandlers(container = document) {
    const images = container.querySelectorAll('img:not([data-broken-handled])');

    images.forEach(img => {
        // Handle images that have already failed to load
        if (img.complete && img.naturalWidth === 0 && img.src) {
            handleBrokenImage(img);
            return;
        }

        // Set up error handler for future failures
        img.addEventListener('error', function() {
            handleBrokenImage(this);
        }, { once: true });
    });
}

/**
 * Initialize broken image handling
 * @param {Element} container - DOM element to search for images
 */
function initBrokenImages(container = document) {
    setupImageErrorHandlers(container);
}

// Export functions for use in main.js
export { initBrokenImages, handleBrokenImage };
