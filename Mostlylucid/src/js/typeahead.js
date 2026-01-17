export  function typeahead() {
    return {
        query: '',
        results: [], // Array of article objects {title, slug, url, score}
        highlightedIndex: -1,

        search() {
            if (this.query.length < 2) {
                this.results = [];
                this.highlightedIndex = -1;
                return;
            }

            // Get matching articles (not just term suggestions)
            fetch(`/api/typeahead/${encodeURIComponent(this.query)}`, {
                method: 'GET',
                headers: { 'Content-Type': 'application/json' }
            })
                .then(response => {
                    if(response.ok){
                        return response.json();
                    }
                    return Promise.reject(response);
                })
                .then(articles => {
                    this.results = articles; // Array of {title, slug, url, score}
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

        // Navigate directly to the highlighted article (used by Tab key)
        completeWithHighlighted() {
            if (this.highlightedIndex >= 0 && this.highlightedIndex < this.results.length) {
                // Navigate to the article
                window.location.href = `/blog/${this.results[this.highlightedIndex].slug}`;
            } else if (this.results.length > 0) {
                // If nothing highlighted, go to first result
                window.location.href = `/blog/${this.results[0].slug}`;
            }
        },

        // Navigate to article or search (used by Enter key)
        completeAndSearch() {
            if (this.highlightedIndex >= 0 && this.highlightedIndex < this.results.length) {
                // Navigate directly to the highlighted article
                window.location.href = `/blog/${this.results[this.highlightedIndex].slug}`;
            } else if (this.results.length > 0) {
                // Go to first result
                window.location.href = `/blog/${this.results[0].slug}`;
            } else {
                // No results, go to search page
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