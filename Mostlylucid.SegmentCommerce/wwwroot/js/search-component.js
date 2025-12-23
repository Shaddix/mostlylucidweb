/**
 * Alpine.js component for the header search bar.
 * Provides real-time search with debounced API calls.
 */
function searchComponent() {
    return {
        query: '',
        results: [],
        loading: false,
        showResults: false,
        abortController: null,

        async search() {
            const q = this.query.trim();
            
            // Don't search if query is too short
            if (q.length < 2) {
                this.results = [];
                this.showResults = false;
                return;
            }

            // Cancel any pending request
            if (this.abortController) {
                this.abortController.abort();
            }
            this.abortController = new AbortController();

            this.loading = true;
            this.showResults = true;

            try {
                const response = await fetch(`/api/search?q=${encodeURIComponent(q)}&limit=10`, {
                    signal: this.abortController.signal,
                    headers: {
                        'Accept': 'application/json',
                        ...this.getTrackingHeaders()
                    }
                });

                if (!response.ok) {
                    throw new Error(`Search failed: ${response.status}`);
                }

                const data = await response.json();
                this.results = data.items || [];
            } catch (error) {
                if (error.name !== 'AbortError') {
                    console.error('Search error:', error);
                    this.results = [];
                }
            } finally {
                this.loading = false;
            }
        },

        goToSearch() {
            const q = this.query.trim();
            if (q.length >= 2) {
                window.location.href = `/products/search?q=${encodeURIComponent(q)}`;
            }
        },

        getTrackingHeaders() {
            // Get tracking headers from TrackingManager if available
            if (typeof TrackingManager !== 'undefined') {
                const state = TrackingManager.getState();
                const headers = {};
                
                if (state.sessionId) {
                    headers['X-Session-ID'] = state.sessionId;
                }
                if (state.fingerprint) {
                    headers['X-Fingerprint'] = state.fingerprint;
                }
                
                return headers;
            }
            return {};
        }
    };
}

// Register globally for Alpine
window.searchComponent = searchComponent;
