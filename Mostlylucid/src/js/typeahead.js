export  function typeahead() {
    return {
        query: '',
        results: [],
        highlightedIndex: -1, // Tracks the currently highlighted index
        popularPosts: null, // Cached popular posts (null = not loaded yet)
        showingPopular: false, // Whether we're showing popular posts vs search results

        // Load popular posts on first focus (cached after first load)
        async loadPopularPosts() {
            if (this.popularPosts !== null) return; // Already loaded

            try {
                const response = await fetch('/api/popular', {
                    method: 'GET',
                    headers: { 'Content-Type': 'application/json' }
                });
                if (response.ok) {
                    this.popularPosts = await response.json();
                } else {
                    this.popularPosts = []; // Mark as loaded but empty
                }
            } catch (e) {
                console.log("Error fetching popular posts");
                this.popularPosts = []; // Mark as loaded but empty
            }
        },

        // Show popular posts when input is focused and empty
        async onFocus() {
            await this.loadPopularPosts();
            if (this.query.length < 2 && this.popularPosts && this.popularPosts.length > 0) {
                this.results = this.popularPosts;
                this.showingPopular = true;
                this.highlightedIndex = -1;
                this.$nextTick(() => {
                    htmx.process(document.getElementById('searchresults'));
                });
            }
        },

        search() {
            if (this.query.length < 2) {
                // Show popular posts if available, otherwise clear
                if (this.popularPosts && this.popularPosts.length > 0) {
                    this.results = this.popularPosts;
                    this.showingPopular = true;
                } else {
                    this.results = [];
                    this.showingPopular = false;
                }
                this.highlightedIndex = -1;
                return;
            }

            this.showingPopular = false;
            fetch(`/api/search/${encodeURIComponent(this.query)}`, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json'
                }
            })
                .then(response => {
                    if(response.ok){
                        return  response.json();
                    }
                    return Promise.reject(response);
                })
                .then(data => {
                    this.results = data;
                    this.highlightedIndex = -1; // Reset index on new search
                    this.$nextTick(() => {
                        htmx.process(document.getElementById('searchresults'));
                    });
                })
                .catch((response) => {
                    console.log(response.status, response.statusText);
                    if(response.status === 400)
                    {
                        console.log('Bad request, reloading page to try to fix it.');
                        window.location.reload();
                    }
                    response.json().then((json) => {
                        console.log(json);
                    })
                    console.log("Error fetching search results");
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

        selectHighlighted() {
            if (this.highlightedIndex >= 0 && this.highlightedIndex < this.results.length) {
                this.selectResult(this.highlightedIndex);
                
            }
        },

        selectResult(selectedIndex) {
            let links = document.querySelectorAll('#searchresults a');
            links[selectedIndex].click();
            this.results = []; // Clear the results
            this.highlightedIndex = -1; // Reset the highlighted index
            this.query = ''; // Clear the query
        },

        goToSearch() {
            if (this.query.length > 0) {
                window.location.href = `/search?query=${encodeURIComponent(this.query)}`;
            }
        }
    }
}