export  function typeahead() {
    return {
        query: '',
        results: [], // Array of term strings (not objects)
        highlightedIndex: -1,

        search() {
            if (this.query.length < 2) {
                this.results = [];
                this.highlightedIndex = -1;
                return;
            }

            // Get term suggestions (not article titles)
            fetch(`/api/suggest/${encodeURIComponent(this.query)}`, {
                method: 'GET',
                headers: { 'Content-Type': 'application/json' }
            })
                .then(response => {
                    if(response.ok){
                        return response.json();
                    }
                    return Promise.reject(response);
                })
                .then(terms => {
                    this.results = terms; // Simple array of strings
                    this.highlightedIndex = -1;
                })
                .catch((error) => {
                    console.error("Autocomplete failed:", error);
                    this.results = [];
                });
        },

        moveDown() {
            if (this.highlightedIndex < this.results.length - 1) {
                this.highlightedIndex++;
            }
        },

        moveUp() {
            if (this.highlightedIndex > 0) {
                this.highlightedIndex--;
            }
        },

        // Complete the input with the highlighted term (used by Tab key)
        completeWithHighlighted() {
            if (this.highlightedIndex >= 0 && this.highlightedIndex < this.results.length) {
                // Replace query with the selected term
                this.query = this.results[this.highlightedIndex];
                this.results = []; // Clear suggestions
                this.highlightedIndex = -1;
                // Focus stays in input so user can continue typing or press Enter to search
            } else if (this.results.length > 0) {
                // If nothing highlighted, use first result
                this.query = this.results[0];
                this.results = [];
                this.highlightedIndex = -1;
            }
        },

        // Complete and immediately search (used by Enter key when item is highlighted)
        completeAndSearch() {
            if (this.highlightedIndex >= 0 && this.highlightedIndex < this.results.length) {
                this.query = this.results[this.highlightedIndex];
                this.results = [];
                this.highlightedIndex = -1;
                this.goToSearch();
            } else if (this.results.length > 0) {
                this.query = this.results[0];
                this.results = [];
                this.highlightedIndex = -1;
                this.goToSearch();
            } else {
                this.goToSearch();
            }
        },

        // Navigate to search page with current query
        goToSearch() {
            if (this.query.length > 0) {
                window.location.href = `/search?query=${encodeURIComponent(this.query)}`;
            }
        }
    }
}