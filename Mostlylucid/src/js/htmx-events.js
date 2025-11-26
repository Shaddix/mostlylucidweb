import { showToast } from './toast';
import "./hx-sweetalert-indicator";




// Existing HTMX error handlers and events
document.body.addEventListener('htmx:responseError', function (event) {
    const status = event.detail.xhr.status;
    let message = 'An unexpected error occurred.';

    if (status === 404) message = 'Resource not found.';
    else if (status === 500) message = 'Server error. Please try again later.';

    showToast(message, 3000, 'error');
});

document.body.addEventListener('stopPolling', function (evt) {
    const container = evt.target.closest('[hx-trigger]');
    if (container) {
        container.removeAttribute('hx-trigger');
        console.log("Polling stopped by trigger.");
    }
});

document.body.addEventListener('htmx:timeout', () => {
    showToast('The request timed out. Please try again.', 3000, 'error');
});

document.body.addEventListener('htmx:sendError', () => {
    showToast('Failed to send request. Please check your connection or try again.', 3000, 'error');
});

document.body.addEventListener("showToast", (event) => {
    const { toast, issuccess } = event.detail || {};
    const type = issuccess === false ? 'error' : 'success';
    showToast(toast || 'Done!', 3000, type);
});

window.addEventListener('htmx:configRequest', ({ detail }) => {
    if (detail.elt?.matches('#spamwords-rrefresh')) {
        const params = new URLSearchParams(window.location.search);
        detail.parameters = {
            ...(detail.parameters || {}),
            ...Object.fromEntries(params.entries())
        };
    }
});