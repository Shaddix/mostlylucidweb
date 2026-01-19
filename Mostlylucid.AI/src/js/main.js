// Initialize namespaces
window.mostlylucidai = window.mostlylucidai || {};

import Alpine from 'alpinejs';
import htmx from "htmx.org";

window.Alpine = Alpine;
window.htmx = htmx;

// Configure HTMX history behavior
htmx.config.refreshOnHistoryMiss = true;

// Global setup for Alpine.js (theme switching)
function globalSetup() {
    return {
        isMobileMenuOpen: false,
        isDarkMode: false,

        themeInit() {
            if (
                localStorage.theme === "dark" ||
                (!("theme" in localStorage) &&
                    window.matchMedia("(prefers-color-scheme: dark)").matches)
            ) {
                localStorage.theme = "dark";
                document.documentElement.classList.add("dark");
                document.documentElement.classList.remove("light");
                this.isDarkMode = true;
            } else {
                localStorage.theme = "light";
                document.documentElement.classList.remove("dark");
                document.documentElement.classList.add("light");
                this.isDarkMode = false;
            }
        },

        themeSwitch() {
            if (localStorage.theme === "dark") {
                localStorage.theme = "light";
                document.documentElement.classList.remove("dark");
                document.documentElement.classList.add("light");
                this.isDarkMode = false;
            } else {
                localStorage.theme = "dark";
                document.documentElement.classList.add("dark");
                document.documentElement.classList.remove("light");
                this.isDarkMode = true;
            }
        },

        toggleMobileMenu() {
            this.isMobileMenuOpen = !this.isMobileMenuOpen;
        }
    };
}

window.globalSetup = globalSetup;

// Track Alpine initialization
let alpineStarted = false;

function startAlpine() {
    if (alpineStarted) return false;

    if (window.Alpine && typeof window.Alpine.start === 'function') {
        try {
            window.Alpine.start();
            alpineStarted = true;
            return true;
        } catch (err) {
            console.error('Alpine start failed:', err.message);
            return false;
        }
    }
    return false;
}

// Start Alpine
if (!startAlpine()) {
    setTimeout(() => startAlpine(), 100);
}

// HTMX event listener for content swaps
function registerHTMXListener() {
    if (typeof window.htmx === 'undefined') {
        setTimeout(registerHTMXListener, 100);
        return;
    }

    document.body.addEventListener('htmx:afterSettle', function(evt) {
        const targetId = evt.detail.target.id;
        if (targetId !== 'contentcontainer' && targetId !== 'main-content') {
            return;
        }

        // Scroll to top after content swap
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }, { once: false });
}

// Toast notification function
function showToast(message, type = 'success', duration = 3000) {
    const toast = document.createElement('div');
    toast.className = `toast-ai ${type === 'error' ? 'bg-red-600' : 'bg-secondary'}`;
    toast.textContent = message;
    document.body.appendChild(toast);

    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transform = 'translateX(100%)';
        setTimeout(() => toast.remove(), 300);
    }, duration);
}

window.showToast = showToast;
window.mostlylucidai.showToast = showToast;

// Contact form handler
function contactForm() {
    return {
        formData: {
            name: '',
            email: '',
            company: '',
            projectType: '',
            message: ''
        },
        submitting: false,
        submitted: false,
        error: null,

        async submitForm() {
            this.submitting = true;
            this.error = null;

            try {
                const response = await fetch('/contact/submit', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'X-CSRF-TOKEN': document.querySelector('input[name="__RequestVerificationToken"]')?.value
                    },
                    body: JSON.stringify(this.formData)
                });

                if (response.ok) {
                    this.submitted = true;
                    showToast('Message sent successfully!', 'success');
                } else {
                    throw new Error('Failed to send message');
                }
            } catch (err) {
                this.error = 'Failed to send message. Please try again.';
                showToast('Failed to send message', 'error');
            } finally {
                this.submitting = false;
            }
        },

        resetForm() {
            this.formData = { name: '', email: '', company: '', projectType: '', message: '' };
            this.submitted = false;
            this.error = null;
        }
    };
}

window.contactForm = contactForm;
window.mostlylucidai.contactForm = contactForm;

// Initialize page
async function initializePage() {
    registerHTMXListener();
}

// Safe initialization
async function safeInitialize() {
    if (document.readyState === 'loading') {
        await new Promise(resolve => {
            document.addEventListener('DOMContentLoaded', resolve, { once: true });
        });
    }
    await initializePage();
}

safeInitialize();
