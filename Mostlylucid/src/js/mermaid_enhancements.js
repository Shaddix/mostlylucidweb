// Mermaid enhancements: SVG pan/zoom and export functionality
'use strict';

import svgPanZoom from 'svg-pan-zoom';
import { toPng, toSvg } from 'html-to-image';

const panZoomInstances = new Map();

/**
 * Create control buttons for a Mermaid diagram
 * @param {HTMLElement} container - The container element for the diagram
 * @param {string} diagramId - Unique ID for the diagram
 */
function createControlButtons(container, diagramId) {
    // Check if controls already exist
    if (container.querySelector('.mermaid-controls')) {
        return;
    }

    const controlsDiv = document.createElement('div');
    controlsDiv.className = 'mermaid-controls';

    const buttons = [
        { icon: 'bx-fullscreen', title: 'Fullscreen', action: 'fullscreen' },
        { icon: 'bx-zoom-in', title: 'Zoom In', action: 'zoomIn' },
        { icon: 'bx-zoom-out', title: 'Zoom Out', action: 'zoomOut' },
        { icon: 'bx-reset', title: 'Reset View', action: 'reset' },
        { icon: 'bx-move', title: 'Pan', action: 'pan' },
        { icon: 'bx-image', title: 'Export as PNG', action: 'exportPng' },
        { icon: 'bx-code-alt', title: 'Export as SVG', action: 'exportSvg' }
    ];

    buttons.forEach(btn => {
        const button = document.createElement('button');
        button.className = `mermaid-control-btn bx ${btn.icon}`;
        button.setAttribute('title', btn.title);
        button.setAttribute('aria-label', btn.title);
        button.setAttribute('data-action', btn.action);
        button.setAttribute('data-diagram-id', diagramId);
        controlsDiv.appendChild(button);
    });

    container.appendChild(controlsDiv);
}

/**
 * Initialize SVG pan/zoom for a diagram
 * @param {HTMLElement} svgElement - The SVG element
 * @param {string} diagramId - Unique ID for the diagram
 */
function initPanZoom(svgElement, diagramId) {
    // Clean up existing instance if present
    if (panZoomInstances.has(diagramId)) {
        try {
            panZoomInstances.get(diagramId).destroy();
        } catch (e) {
            console.warn('Failed to destroy existing pan-zoom instance:', e);
        }
        panZoomInstances.delete(diagramId);
    }

    try {
        const panZoomInstance = svgPanZoom(svgElement, {
            zoomEnabled: true,
            controlIconsEnabled: false, // We'll use our custom controls
            fit: true,
            center: true,
            minZoom: 0.1,
            maxZoom: 10,
            zoomScaleSensitivity: 0.3,
            dblClickZoomEnabled: true,
            mouseWheelZoomEnabled: true,
            preventMouseEventsDefault: true,
            contain: false
        });

        panZoomInstances.set(diagramId, panZoomInstance);
        return panZoomInstance;
    } catch (error) {
        console.error('Failed to initialize pan-zoom:', error);
        return null;
    }
}

/**
 * Handle control button clicks
 * @param {Event} event - Click event
 */
function handleControlClick(event) {
    const button = event.target.closest('.mermaid-control-btn');
    if (!button) return;

    const action = button.getAttribute('data-action');
    const diagramId = button.getAttribute('data-diagram-id');
    const panZoomInstance = panZoomInstances.get(diagramId);

    // Find container - could be .mermaid-wrapper (in-page) or .mermaid-lightbox-diagram-wrapper (lightbox)
    const container = button.closest('.mermaid-wrapper') || button.closest('.mermaid-lightbox-diagram-wrapper');

    switch (action) {
        case 'fullscreen':
            // Fullscreen only works for in-page diagrams
            if (container?.classList.contains('mermaid-wrapper')) {
                openFullscreenLightbox(container, diagramId);
            }
            break;
        case 'zoomIn':
            if (panZoomInstance) panZoomInstance.zoomIn();
            break;
        case 'zoomOut':
            if (panZoomInstance) panZoomInstance.zoomOut();
            break;
        case 'reset':
            if (panZoomInstance) {
                panZoomInstance.reset();
                panZoomInstance.center();
                panZoomInstance.fit();
            }
            break;
        case 'pan':
            if (panZoomInstance) {
                const isPanEnabled = panZoomInstance.isPanEnabled();
                panZoomInstance.enablePan(!isPanEnabled);
                button.classList.toggle('active', !isPanEnabled);
            }
            break;
        case 'exportPng':
            if (container) exportDiagram(container, 'png', diagramId);
            break;
        case 'exportSvg':
            if (container) exportDiagram(container, 'svg', diagramId);
            break;
    }
}

/**
 * Open diagram in fullscreen lightbox
 * @param {HTMLElement} container - The diagram container
 * @param {string} diagramId - Unique ID for the diagram
 */
function openFullscreenLightbox(container, diagramId) {
    // Find the SVG element
    const svgElement = container.querySelector('svg');
    if (!svgElement) return;

    // Create lightbox overlay
    const lightbox = document.createElement('div');
    lightbox.className = 'mermaid-lightbox';
    lightbox.innerHTML = `
        <div class="mermaid-lightbox-content">
            <button class="mermaid-lightbox-close bx bx-x" aria-label="Close"></button>
            <div class="mermaid-lightbox-diagram-wrapper">
                <div class="mermaid-lightbox-diagram"></div>
            </div>
        </div>
    `;

    // Clone the SVG
    const clonedSvg = svgElement.cloneNode(true);

    // Remove any inline size constraints
    clonedSvg.removeAttribute('width');
    clonedSvg.removeAttribute('height');
    clonedSvg.style.width = '100%';
    clonedSvg.style.height = '100%';

    const diagramContainer = lightbox.querySelector('.mermaid-lightbox-diagram');
    diagramContainer.appendChild(clonedSvg);

    // Add controls to the lightbox wrapper
    const wrapper = lightbox.querySelector('.mermaid-lightbox-diagram-wrapper');
    const lightboxDiagramId = `${diagramId}-lightbox`;
    createControlButtons(wrapper, lightboxDiagramId);

    // Add to body
    document.body.appendChild(lightbox);

    // Wait for layout to complete before initializing pan-zoom
    setTimeout(() => {
        const panZoom = initPanZoom(clonedSvg, lightboxDiagramId);
        if (panZoom) {
            // Force the diagram to fit the container
            panZoom.resize();
            panZoom.fit();
            panZoom.center();
        }
    }, 100);

    // Close handlers
    const closeLightbox = () => {
        // Clean up pan-zoom instance
        if (panZoomInstances.has(lightboxDiagramId)) {
            try {
                panZoomInstances.get(lightboxDiagramId).destroy();
            } catch (e) {
                console.warn('Failed to destroy lightbox pan-zoom instance:', e);
            }
            panZoomInstances.delete(lightboxDiagramId);
        }

        lightbox.remove();
    };

    lightbox.querySelector('.mermaid-lightbox-close').addEventListener('click', closeLightbox);
    lightbox.addEventListener('click', (e) => {
        if (e.target === lightbox) closeLightbox();
    });

    // ESC key to close
    const escHandler = (e) => {
        if (e.key === 'Escape') {
            closeLightbox();
            document.removeEventListener('keydown', escHandler);
        }
    };
    document.addEventListener('keydown', escHandler);
}

/**
 * Export diagram as PNG or SVG
 * @param {HTMLElement} container - The diagram container
 * @param {string} format - Export format ('png' or 'svg')
 * @param {string} diagramId - Unique ID for the diagram
 */
async function exportDiagram(container, format, diagramId) {
    try {
        // Find the SVG element
        const svgElement = container.querySelector('svg');
        if (!svgElement) {
            window.showToast && window.showToast('No diagram found to export', 3000, 'error');
            return;
        }

        // Clone the SVG to avoid modifying the original
        const clonedSvg = svgElement.cloneNode(true);

        // Get the viewBox or calculate from bounding box
        let viewBox = clonedSvg.getAttribute('viewBox');
        if (!viewBox) {
            const bbox = svgElement.getBBox();
            viewBox = `${bbox.x} ${bbox.y} ${bbox.width} ${bbox.height}`;
            clonedSvg.setAttribute('viewBox', viewBox);
        }

        // Parse viewBox to get dimensions
        const [, , vbWidth, vbHeight] = viewBox.split(' ').map(Number);

        // Set explicit dimensions based on viewBox for proper export
        clonedSvg.setAttribute('width', vbWidth);
        clonedSvg.setAttribute('height', vbHeight);

        // Remove inline styles (pan-zoom transforms) but keep viewBox
        clonedSvg.removeAttribute('style');
        clonedSvg.style.backgroundColor = 'transparent';
        clonedSvg.style.maxWidth = 'none';

        // Create a temporary container
        const tempDiv = document.createElement('div');
        tempDiv.style.position = 'absolute';
        tempDiv.style.left = '-9999px';
        tempDiv.appendChild(clonedSvg);
        document.body.appendChild(tempDiv);

        let dataUrl;
        const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
        const filename = `mermaid-diagram-${timestamp}`;

        if (format === 'png') {
            dataUrl = await toPng(clonedSvg, {
                backgroundColor: 'white',
                pixelRatio: 2 // Higher quality
            });
            downloadFile(dataUrl, `${filename}.png`);
        } else {
            dataUrl = await toSvg(clonedSvg, {
                backgroundColor: 'transparent'
            });
            downloadFile(dataUrl, `${filename}.svg`);
        }

        // Clean up
        document.body.removeChild(tempDiv);

        window.showToast && window.showToast(`Diagram exported as ${format.toUpperCase()}`, 3000, 'success');
    } catch (error) {
        console.error('Failed to export diagram:', error);
        window.showToast && window.showToast('Failed to export diagram', 3000, 'error');
    }
}

/**
 * Trigger file download
 * @param {string} dataUrl - Data URL
 * @param {string} filename - Download filename
 */
function downloadFile(dataUrl, filename) {
    const link = document.createElement('a');
    link.download = filename;
    link.href = dataUrl;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
}

/**
 * Wrap diagram in container with controls
 * @param {HTMLElement} diagramElement - The diagram element
 * @returns {string} Unique diagram ID
 */
function wrapDiagramWithControls(diagramElement) {
    // Check if already wrapped
    if (diagramElement.closest('.mermaid-wrapper')) {
        return diagramElement.closest('.mermaid-wrapper').getAttribute('data-diagram-id');
    }

    const diagramId = `mermaid-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;

    const wrapper = document.createElement('div');
    wrapper.className = 'mermaid-wrapper';
    wrapper.setAttribute('data-diagram-id', diagramId);

    // Insert wrapper before the diagram
    diagramElement.parentNode.insertBefore(wrapper, diagramElement);

    // Move diagram into wrapper
    wrapper.appendChild(diagramElement);

    // Create controls
    createControlButtons(wrapper, diagramId);

    return diagramId;
}

/**
 * Enhance all Mermaid diagrams on the page
 */
export function enhanceMermaidDiagrams() {
    const diagrams = document.querySelectorAll('.mermaid[data-processed="true"]');

    diagrams.forEach(diagram => {
        const svgElement = diagram.querySelector('svg');
        if (!svgElement) return;

        // Remove inline max-width constraint that Mermaid adds
        svgElement.style.maxWidth = 'none';

        // Wrap diagram with controls
        const diagramId = wrapDiagramWithControls(diagram);

        // Initialize pan/zoom and fit to view
        const panZoom = initPanZoom(svgElement, diagramId);
        if (panZoom) {
            // Fit the diagram to the container by default so entire diagram is visible
            setTimeout(() => {
                panZoom.resize();
                panZoom.fit();
                panZoom.center();
            }, 100);
        }
    });

    // Set up event delegation for control buttons (only once)
    if (!document.body.hasAttribute('data-mermaid-controls-initialized')) {
        document.body.addEventListener('click', handleControlClick);
        document.body.setAttribute('data-mermaid-controls-initialized', 'true');
    }
}

/**
 * Clean up all pan-zoom instances
 */
export function cleanupMermaidEnhancements() {
    panZoomInstances.forEach((instance, id) => {
        try {
            instance.destroy();
        } catch (e) {
            console.warn(`Failed to destroy pan-zoom instance ${id}:`, e);
        }
    });
    panZoomInstances.clear();
}

// Export for global access
if (typeof window !== 'undefined') {
    window.enhanceMermaidDiagrams = enhanceMermaidDiagrams;
    window.cleanupMermaidEnhancements = cleanupMermaidEnhancements;
}
