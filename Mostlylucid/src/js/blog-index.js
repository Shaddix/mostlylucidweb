// Blog index interactions (date range, language/order selects, calendar highlights)
// Designed to re-initialize after HTMX swaps.
(function(){
  function formatYMD(d){
    const iso = d.toISOString();
    return iso.substring(0,10);
  }

  // Cache for month highlights to avoid refetching
  const highlightCache = new Map();

  function getCacheKey(year, month, language){
    return `${year}-${month}-${language || 'en'}`;
  }

  async function fetchMonth(year, month, language){
    const lang = encodeURIComponent(language || 'en');
    const cacheKey = getCacheKey(year, month, lang);

    // Check cache first
    if (highlightCache.has(cacheKey)) {
      console.log('Using cached highlights for', cacheKey);
      return highlightCache.get(cacheKey);
    }

    try{
      console.log('Fetching calendar highlights for', year, month, lang);
      const res = await fetch(`/blog/calendar-days?year=${year}&month=${month}&language=${lang}`);
      if(!res.ok) return new Set();
      const j = await res.json();
      const dates = new Set(j.dates || []);
      // Cache the result
      highlightCache.set(cacheKey, dates);
      return dates;
    }catch(e){
      console.warn('Error fetching calendar highlights:', e);
      return new Set();
    }
  }

  // Fetch highlights for multiple months (for showMonths: 2)
  async function initMonthHighlights(fp, language){
    const y = fp.currentYear;
    const m = fp.currentMonth + 1; // 0-based in flatpickr

    // Fetch current month and next month (for 2-month display)
    const promises = [
      fetchMonth(y, m, language)
    ];

    // Calculate next month
    let nextMonth = m + 1;
    let nextYear = y;
    if (nextMonth > 12) {
      nextMonth = 1;
      nextYear = y + 1;
    }
    promises.push(fetchMonth(nextYear, nextMonth, language));

    // Combine results from both months
    const results = await Promise.all(promises);
    const combined = new Set();
    results.forEach(set => {
      set.forEach(date => combined.add(date));
    });

    return combined;
  }

  // Clear cache when language changes
  function clearHighlightCache(){
    highlightCache.clear();
  }

  // Cache for date range
  const dateRangeCache = new Map();

  async function fetchDateRange(language){
    const lang = encodeURIComponent(language || 'en');
    const cacheKey = `daterange-${lang}`;

    // Check cache first
    if (dateRangeCache.has(cacheKey)) {
      console.log('Using cached date range for', lang);
      return dateRangeCache.get(cacheKey);
    }

    try {
      console.log('Fetching date range for language:', lang);
      const res = await fetch(`/blog/date-range?language=${lang}`);
      if (!res.ok) {
        console.warn('Date range fetch failed:', res.status);
        return null;
      }
      const data = await res.json();
      console.log('Received date range:', data);
      // Cache the result
      dateRangeCache.set(cacheKey, data);
      return data;
    } catch(e) {
      console.error('Error fetching date range:', e);
      return null;
    }
  }

  // Clear date range cache when language changes
  function clearDateRangeCache(){
    dateRangeCache.clear();
  }

  function applyNavigation(u){
    const target = document.querySelector('#content');
    console.log('=== APPLYING NAVIGATION ===');
    console.log('URL:', u.toString());
    console.log('All params:', Object.fromEntries(u.searchParams.entries()));

    // Always push the URL so user can navigate back/forward
    try{ window.history.pushState({}, '', u.toString()); }catch{}

    if(window.htmx && target){
      console.log('Making HTMX request to:', u.toString());
      window.htmx.ajax('GET', u.toString(), {
        target: '#content',
        swap: 'outerHTML show:none',
        headers: {'pagerequest': 'true'}
      });
    } else {
      console.log('HTMX not available, doing full page load');
      window.location.href = u.toString();
    }
  }

  function initFromRoot(root, forceReinit = false){
    // Prevent double initialization on the same root (Rocket Loader protection)
    // But allow forced re-init (e.g., after HTMX swap)
    const now = Date.now();
    if (root.__blogIndexInitialized && !forceReinit) {
      const timeSinceInit = now - (root.__blogIndexInitTime || 0);
      // If initialized less than 100ms ago, it's likely a duplicate call - skip it
      if (timeSinceInit < 100) {
        console.log('Blog index recently initialized, skipping duplicate call');
        return;
      }
    }
    root.__blogIndexInitialized = true;
    root.__blogIndexInitTime = now;
    console.log('Initializing blog index on root:', root.id || 'document');

    function formatDateForDisplay(dateStr) {
      try {
        const date = new Date(dateStr);
        return date.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
      } catch {
        return dateStr;
      }
    }

    function updateSummary(){
      const summaryEl = document.querySelector('#filterSummary');
      if(!summaryEl) return;
      const url = new URL(window.location.href);

      // Get language from URL, dropdown, localStorage, or cookie
      const fromUrl = url.searchParams.get('language');
      const fromDropdown = document.querySelector('#languageSelect')?.value;
      const fromStorage = localStorage.getItem('preferredLanguage');
      const fromCookie = getCookie('preferredLanguage');
      const language = (fromUrl || fromDropdown || fromStorage || fromCookie || 'en').toLowerCase();

      const orderBy = (url.searchParams.get('orderBy') || 'date').toLowerCase();
      const orderDir = (url.searchParams.get('orderDir') || 'desc').toLowerCase();
      const start = url.searchParams.get('startDate');
      const end = url.searchParams.get('endDate');

      // Language code to name mapping
      const langMap = {
        'en': 'English', 'es': 'Spanish', 'fr': 'French', 'de': 'German',
        'it': 'Italian', 'nl': 'Dutch', 'sv': 'Swedish', 'fi': 'Finnish',
        'ar': 'Arabic', 'hi': 'Hindi', 'zh': 'Chinese', 'jap': 'Japanese',
        'el': 'Greek', 'uk': 'Ukrainian'
      };

      const orderMap = {
        'date_desc':'Newest first',
        'date_asc':'Oldest first',
        'title_asc':'Title A–Z',
        'title_desc':'Title Z–A'
      };

      const ordKey = `${orderBy}_${orderDir}`;
      const parts = [];
      const langName = langMap[language.toLowerCase()] || language.toUpperCase();
      parts.push(`<span class="font-medium">${langName}</span>`);
      parts.push(`<span class="text-gray-500 dark:text-gray-400">${orderMap[ordKey] || 'Custom order'}</span>`);
      if(start && end){
        const startFormatted = formatDateForDisplay(start);
        const endFormatted = formatDateForDisplay(end);
        parts.push(`<span class="text-blue-600 dark:text-blue-400">${startFormatted} → ${endFormatted}</span>`);
      }
      summaryEl.innerHTML = parts.join(' <span class="text-gray-400">•</span> ');
    }

    function hookThemeObserver(){
      try{
        const htmlEl = document.documentElement;
        const obs = new MutationObserver(() => {
          const dr = document.querySelector('#dateRange');
          const fp = dr && dr._flatpickr;
          if(fp && typeof fp.redraw === 'function'){
            fp.redraw();
          }
        });
        obs.observe(htmlEl, { attributes:true, attributeFilter:['class'] });
      }catch{}
    }
    // Filter elements are outside #content, so search from document
    const input = document.querySelector('#dateRange');
    const clearBtn = document.querySelector('#clearDateFilter');
    const langSelect = document.querySelector('#languageSelect');
    const orderSelect = document.querySelector('#orderSelect');
    const categorySelect = document.querySelector('#categorySelect');

    console.log('initFromRoot - Found elements:', {
      input: !!input,
      clearBtn: !!clearBtn,
      langSelect: !!langSelect,
      orderSelect: !!orderSelect,
      categorySelect: !!categorySelect,
      rootId: root.id
    });

    const url = new URL(window.location.href);
    const existingStart = url.searchParams.get('startDate');
    const existingEnd = url.searchParams.get('endDate');

    // Get language from URL, localStorage, or cookie (same priority as dropdown)
    const fromUrl = url.searchParams.get('language');
    const fromStorage = localStorage.getItem('preferredLanguage');
    const fromCookie = getCookie('preferredLanguage');
    const existingLang = (fromUrl || fromStorage || fromCookie || 'en').toLowerCase();

    const existingOrderBy = (url.searchParams.get('orderBy') || 'date').toLowerCase();
    const existingOrderDir = (url.searchParams.get('orderDir') || 'desc').toLowerCase();
    const existingCategory = url.searchParams.get('category') || '';

    // Helper function to get cookie
    function getCookie(name) {
      const value = `; ${document.cookie}`;
      const parts = value.split(`; ${name}=`);
      if (parts.length === 2) return parts.pop().split(';').shift();
      return null;
    }

    console.log('Initializing filters from URL:', {
      lang: existingLang,
      orderBy: existingOrderBy,
      orderDir: existingOrderDir,
      fullOrder: `${existingOrderBy}_${existingOrderDir}`
    });

    if (langSelect) {
      langSelect.value = existingLang;
      console.log('Set language select to:', langSelect.value);
    }
    if (orderSelect) {
      orderSelect.value = `${existingOrderBy}_${existingOrderDir}`;
      console.log('Set order select to:', orderSelect.value);
    }
    if (categorySelect) {
      categorySelect.value = existingCategory;
      console.log('Set category select to:', categorySelect.value);
    }
    updateSummary();
    hookThemeObserver();

    let highlightDates = new Set();

    if (input && window.flatpickr){
      // Destroy existing flatpickr instance if it exists
      if (input._flatpickr) {
        console.log('Destroying existing flatpickr instance');
        try {
          input._flatpickr.destroy();
          // Clean up the reference
          delete input._flatpickr;
        } catch(e) {
          console.warn('Error destroying flatpickr:', e);
        }
      }

      // Additional safety check - if flatpickr somehow still exists, bail out
      if (input._flatpickr) {
        console.warn('Flatpickr instance still exists after destroy attempt - skipping re-init');
        return;
      }

      console.log('Creating new flatpickr instance with dates:', existingStart, existingEnd);

      // Fetch date range asynchronously and create flatpickr when ready
      (async () => {
        try {
          // Fetch the date range for this language
          const dateRange = await fetchDateRange(existingLang);

          console.log('=== FLATPICKR DATE RANGE CONFIGURATION ===');
          console.log('Language:', existingLang);
          console.log('Date range received:', dateRange);

          const fpConfig = {
            mode: 'range',
            dateFormat: 'Y-m-d', // ISO format for posting to server
            altInput: true, // Use alternate input for display
            altFormat: 'M j, Y', // Locale-friendly display format (e.g., "Jan 15, 2025")
            showMonths: 2, // Show 2 months at once to reduce empty months
            defaultDate: [existingStart, existingEnd].filter(Boolean),
            onDayCreate: function(dObj, dStr, fpInstance, dayElem){
              const date = dayElem.dateObj;
              if(!date) return;
              const ymd = formatYMD(date);
              if(highlightDates.has(ymd)){
                dayElem.classList.add('has-post');
                dayElem.style.background = 'rgba(76,175,80,0.35)';
                dayElem.style.borderRadius = '6px';
              }
            },
            onMonthChange: async function(selectedDates, dateStr, fpInstance){
              try {
                highlightDates = await initMonthHighlights(fpInstance, (langSelect && langSelect.value) || existingLang);
                // Check instance still exists before redrawing
                if (input._flatpickr === fpInstance && typeof fpInstance.redraw === 'function') {
                  fpInstance.redraw();
                }
              } catch(e) {
                console.warn('Error in onMonthChange:', e);
              }
            },
            onOpen: async function(selectedDates, dateStr, fpInstance){
              try {
                highlightDates = await initMonthHighlights(fpInstance, (langSelect && langSelect.value) || existingLang);
                // Check instance still exists before redrawing
                if (input._flatpickr === fpInstance && typeof fpInstance.redraw === 'function') {
                  fpInstance.redraw();
                }
              } catch(e) {
                console.warn('Error in onOpen:', e);
              }
            },
            onChange: function(selectedDates){
              if(selectedDates.length === 2){
                const [start,end] = selectedDates;
                const startStr = formatYMD(start);
                const endStr = formatYMD(end);
                console.log('Date range selected:', startStr, 'to', endStr);
                const u = new URL(window.location.href);
                u.searchParams.set('startDate', startStr);
                u.searchParams.set('endDate', endStr);
                u.searchParams.set('page', '1'); // Reset to page 1 when changing filters
                if(langSelect) u.searchParams.set('language', langSelect.value);
                const ord = (orderSelect && orderSelect.value) || 'date_desc';
                const [ob,od] = ord.split('_');
                u.searchParams.set('orderBy', ob);
                u.searchParams.set('orderDir', od);
                updateSummary();
                applyNavigation(u);
              }
            }
          };

          // Apply min/max date if we got valid data
          if (dateRange && dateRange.minDate && dateRange.maxDate) {
            fpConfig.minDate = dateRange.minDate;
            fpConfig.maxDate = dateRange.maxDate;
            console.log('Applied date limits to flatpickr:', fpConfig.minDate, 'to', fpConfig.maxDate);
          } else {
            console.warn('No valid date range received, calendar will be unlimited');
          }

          const fp = window.flatpickr(input, fpConfig);

          // Verify min/max date configuration
          console.log('Flatpickr config verification:', {
            minDate: fp.config.minDate,
            maxDate: fp.config.maxDate,
            minDateStr: fp.config.minDate ? formatYMD(new Date(fp.config.minDate)) : 'none',
            maxDateStr: fp.config.maxDate ? formatYMD(new Date(fp.config.maxDate)) : 'none'
          });

          // Preload month highlights after init
          (async ()=>{
            highlightDates = await initMonthHighlights(fp, (langSelect && langSelect.value) || existingLang);
            try {
                // Check instance still exists and hasn't been destroyed
                if (input._flatpickr && input._flatpickr === fp && typeof fp.redraw === 'function') {
                    fp.redraw();
                }
            }
            catch(e)
            {
                console.warn('Error redrawing flatpickr after highlight init:', e);
            }
          })();

          console.log('Flatpickr instance created successfully:', !!fp, 'on input:', input.id);

          // Verify the instance is actually attached
          setTimeout(() => {
            console.log('Flatpickr verification after 100ms:', {
              hasInstance: !!input._flatpickr,
              instanceType: typeof input._flatpickr,
              inputValue: input.value,
              selectedDates: input._flatpickr?.selectedDates?.length,
              minDate: input._flatpickr?.config?.minDate ? formatYMD(new Date(input._flatpickr.config.minDate)) : 'none',
              maxDate: input._flatpickr?.config?.maxDate ? formatYMD(new Date(input._flatpickr.config.maxDate)) : 'none'
            });
          }, 100);
        } catch(e) {
          console.error('Error creating flatpickr instance:', e);
        }
      })();
    } else {
      console.warn('Cannot create flatpickr:', {
        hasInput: !!input,
        hasFlatpickr: !!window.flatpickr,
        inputId: input?.id
      });
    }

    clearBtn && clearBtn.addEventListener('click', function(){
      console.log('Clearing date filter');
      // Clear the flatpickr input
      if(input && input._flatpickr){
        input._flatpickr.clear();
      }
      const u = new URL(window.location.href);
      u.searchParams.delete('startDate');
      u.searchParams.delete('endDate');
      u.searchParams.set('page', '1'); // Reset to page 1 when clearing filters
      if(langSelect) u.searchParams.set('language', langSelect.value);
      const ord = (orderSelect && orderSelect.value) || 'date_desc';
      const [ob,od] = ord.split('_');
      u.searchParams.set('orderBy', ob);
      u.searchParams.set('orderDir', od);
      updateSummary();
      applyNavigation(u);
    });

    langSelect && langSelect.addEventListener('change', async function(){
      console.log('Language changed to:', langSelect.value);

      // Clear caches when language changes
      clearHighlightCache();
      clearDateRangeCache();

      // Start with current URL - this preserves ALL existing parameters including dates
      const u = new URL(window.location.href);
      // Only modify what's changing
      u.searchParams.set('language', langSelect.value);
      u.searchParams.set('page', '1'); // Reset to page 1 when changing filters
      // Make sure order is set from dropdown
      const ord = (orderSelect && orderSelect.value) || 'date_desc';
      const [ob,od] = ord.split('_');
      u.searchParams.set('orderBy', ob);
      u.searchParams.set('orderDir', od);

      // IMPORTANT: Also check flatpickr for selected dates and preserve them
      if(input && input._flatpickr && input._flatpickr.selectedDates.length === 2){
        const [start, end] = input._flatpickr.selectedDates;
        u.searchParams.set('startDate', formatYMD(start));
        u.searchParams.set('endDate', formatYMD(end));
        console.log('Preserving dates from flatpickr:', formatYMD(start), formatYMD(end));
      }

      console.log('Language change - URL params:', Object.fromEntries(u.searchParams.entries()));
      updateSummary();

      // Update flatpickr date limits and highlights for the new language
      if(input && input._flatpickr){
        try{
          const fp = input._flatpickr;

          // Fetch new date range for this language
          const dateRange = await fetchDateRange(langSelect.value);
          if (dateRange && dateRange.minDate && dateRange.maxDate) {
            console.log('Updating flatpickr date limits for language:', langSelect.value, dateRange);
            fp.set('minDate', dateRange.minDate);
            fp.set('maxDate', dateRange.maxDate);
            console.log('New date limits applied:', dateRange.minDate, 'to', dateRange.maxDate);
          }

          // Fetch highlights for both visible months with new language
          highlightDates = await initMonthHighlights(fp, langSelect.value);
          // Check instance still exists before redrawing
          if (input._flatpickr === fp && typeof fp.redraw === 'function') {
            fp.redraw();
          }
        }catch(err){
          console.warn('Unable to refresh calendar for language change', err);
        }
      }

      applyNavigation(u);
    });

    orderSelect && orderSelect.addEventListener('change', function(){
      console.log('Order changed to:', orderSelect.value);
      // Start with current URL - this preserves ALL existing parameters including dates
      const u = new URL(window.location.href);
      // Only modify what's changing
      const [ob,od] = orderSelect.value.split('_');
      u.searchParams.set('orderBy', ob);
      u.searchParams.set('orderDir', od);
      u.searchParams.set('page', '1'); // Reset to page 1 when changing filters
      // Make sure language is set from dropdown
      if(langSelect) u.searchParams.set('language', langSelect.value);

      // IMPORTANT: Also check flatpickr for selected dates and preserve them
      if(input && input._flatpickr && input._flatpickr.selectedDates.length === 2){
        const [start, end] = input._flatpickr.selectedDates;
        u.searchParams.set('startDate', formatYMD(start));
        u.searchParams.set('endDate', formatYMD(end));
        console.log('Preserving dates from flatpickr:', formatYMD(start), formatYMD(end));
      }

      console.log('Order change - URL params:', Object.fromEntries(u.searchParams.entries()));
      updateSummary();
      applyNavigation(u);
    });

    categorySelect && categorySelect.addEventListener('change', function(){
      console.log('Category changed to:', categorySelect.value);
      const u = new URL(window.location.href);
      if(categorySelect.value) {
        u.searchParams.set('category', categorySelect.value);
      } else {
        u.searchParams.delete('category');
      }
      u.searchParams.set('page', '1');
      if(langSelect) u.searchParams.set('language', langSelect.value);
      const ord = (orderSelect && orderSelect.value) || 'date_desc';
      const [ob,od] = ord.split('_');
      u.searchParams.set('orderBy', ob);
      u.searchParams.set('orderDir', od);
      if(input && input._flatpickr && input._flatpickr.selectedDates.length === 2){
        const [start, end] = input._flatpickr.selectedDates;
        u.searchParams.set('startDate', formatYMD(start));
        u.searchParams.set('endDate', formatYMD(end));
      }
      updateSummary();
      applyNavigation(u);
    });

    // Helper: read published dates from posts in the content area and return sorted array of Date objects
    function getPostDates(){
      const posts = Array.from(root.querySelectorAll('[data-published-date]'));
      return posts.map(el => {
        const v = el.getAttribute('data-published-date');
        return v ? new Date(v + 'T00:00:00') : null;
      }).filter(Boolean).sort((a,b) => a - b);
    }

    // Ensure selected range covers returned posts; if not, set selected to last post date in range
    function ensureSelectionCoversPosts(){
      if(!(input && input._flatpickr)) return;
      const fp = input._flatpickr;
      const selected = fp.selectedDates || [];
      if(selected.length === 2){
        const [s,e] = selected;
        const posts = getPostDates();
        if(posts.length === 0) return;
        const lastPost = posts[posts.length-1];
        // If lastPost is outside the selected range, set selection to that date
        if(lastPost < s || lastPost > e){
          console.log('Adjusting selection to include last post date:', formatYMD(lastPost));
          fp.setDate([formatYMD(lastPost), formatYMD(lastPost)], true);
          updateSummary();
        }
      }
    }

    // Store reference to ensureSelectionCoversPosts for global listener
    root.__ensureSelectionCoversPosts = ensureSelectionCoversPosts;
  }

  // Expose Alpine component for categories panel (legacy)
  window.blogCategories = function(){
    return { openCategories: false };
  };

  // Global filter change handler for inline onchange attributes
  window.handleFilterChange = function(type, value) {
    console.log('handleFilterChange:', type, value);
    const u = new URL(window.location.href);
    const langSelect = document.querySelector('#languageSelect');
    const orderSelect = document.querySelector('#orderSelect');
    const categorySelect = document.querySelector('#categorySelect');
    const dateInput = document.querySelector('#dateRange');

    if (type === 'category') {
      if (value) {
        u.searchParams.set('category', value);
      } else {
        u.searchParams.delete('category');
      }
    } else if (type === 'order') {
      const [ob, od] = value.split('_');
      u.searchParams.set('orderBy', ob);
      u.searchParams.set('orderDir', od);
    }

    // Always reset to page 1
    u.searchParams.set('page', '1');

    // Preserve language
    if (langSelect) u.searchParams.set('language', langSelect.value);

    // Preserve order (if not changing it)
    if (type !== 'order' && orderSelect) {
      const ord = orderSelect.value || 'date_desc';
      const [ob, od] = ord.split('_');
      u.searchParams.set('orderBy', ob);
      u.searchParams.set('orderDir', od);
    }

    // Preserve dates from flatpickr
    if (dateInput && dateInput._flatpickr && dateInput._flatpickr.selectedDates.length === 2) {
      const [start, end] = dateInput._flatpickr.selectedDates;
      u.searchParams.set('startDate', formatYMD(start));
      u.searchParams.set('endDate', formatYMD(end));
    }

    applyNavigation(u);
  };

  // Expose init function globally for manual or external calls
  window.blogIndexInit = initFromRoot;

  // Safe initialization that handles Rocket Loader and various loading states
  function safeInit() {
    try {
      console.log('safeInit called, readyState:', document.readyState);
      const root = document.querySelector('#content') || document;
      if (root) {
        initFromRoot(root);
      } else {
        console.warn('No root element found for blog-index initialization');
      }
    } catch(e) {
      console.error('Error in safeInit:', e);
    }
  }

  // Bind listeners once (protect against Rocket Loader multiple execution)
  if (!window.__blogIndexBound) {
    window.__blogIndexBound = true;

    // Initialize based on current document state
    if (document.readyState === 'loading') {
      // DOM not ready yet, wait for DOMContentLoaded
      console.log('DOM loading - adding DOMContentLoaded listener');
      document.addEventListener('DOMContentLoaded', safeInit);
    } else {
      // DOM already loaded (handles Rocket Loader delay)
      console.log('DOM already loaded - initializing immediately');
      // Small delay to ensure all scripts are ready
      setTimeout(safeInit, 0);
    }

    // Reinitialize after HTMX settles (DOM is ready for manipulation)
    // Use a function that safely adds the listener even if body doesn't exist yet
    function addHtmxListener() {
      const target = document.body || document;
      target.addEventListener('htmx:afterSettle', function(){
      try{
          console.log('HTMX afterSettle fired');
        const target = document.querySelector("#content")
        if (!target) {
          console.log('HTMX afterSettle - no target');
          return;
        }

        console.log('HTMX afterSettle - target:', target.id, target);

        // Check if this is the #content element or contains it
        if (target.id === 'content') {
          console.log('Re-initializing blog filters - target IS content');
          // Give DOM a bit more time to fully settle before initializing flatpickr
          setTimeout(() => {
            console.log('Running initFromRoot after delay');
            initFromRoot(target, true); // forceReinit=true for HTMX swaps

            // After init, ensure selection covers posts
            setTimeout(() => {
              if (typeof target.__ensureSelectionCoversPosts === 'function') {
                try {
                  target.__ensureSelectionCoversPosts();
                } catch(e) {
                  console.warn('ensureSelectionCoversPosts failed', e);
                }
              }
            }, 100);
          }, 50);
        } else if (target.querySelector?.('#content')) {
          console.log('Re-initializing blog filters - target CONTAINS content');
          const contentDiv = target.querySelector('#content');
          setTimeout(() => {
            console.log('Running initFromRoot after delay');
            initFromRoot(contentDiv, true); // forceReinit=true for HTMX swaps

            // After init, ensure selection covers posts
            setTimeout(() => {
              if (typeof contentDiv.__ensureSelectionCoversPosts === 'function') {
                try {
                  contentDiv.__ensureSelectionCoversPosts();
                } catch(e) {
                  console.warn('ensureSelectionCoversPosts failed', e);
                }
              }
            }, 100);
          }, 50);
        } else {
          console.log('Skipping reinitialization - not blog content');
        }
      }catch(err){
        console.error('Error reinitializing blog filters:', err);
      }
    });
    }

    // Add HTMX listener - safely handle if body doesn't exist yet
    if (document.body) {
      addHtmxListener();
    } else {
      // Body doesn't exist yet (unlikely but possible with Rocket Loader)
      document.addEventListener('DOMContentLoaded', addHtmxListener);
    }
  }
})();
