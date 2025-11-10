// Blog index interactions (date range, language/order selects, calendar highlights)
// Designed to re-initialize after HTMX swaps.
(function(){
  function formatYMD(d){
    const iso = d.toISOString();
    return iso.substring(0,10);
  }

  async function fetchMonth(year, month, language){
    const lang = encodeURIComponent(language || 'en');
    try{
      const res = await fetch(`/blog/calendar-days?year=${year}&month=${month}&language=${lang}`);
      if(!res.ok) return new Set();
      const j = await res.json();
      return new Set(j.dates || []);
    }catch{ return new Set(); }
  }

  async function initMonthHighlights(fp, language){
    const y = fp.currentYear;
    const m = fp.currentMonth + 1; // 0-based in flatpickr
    return await fetchMonth(y, m, language);
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

  function initFromRoot(root){
    function updateSummary(){
      const summaryEl = root.querySelector('#filterSummary');
      if(!summaryEl) return;
      const url = new URL(window.location.href);
      const language = url.searchParams.get('language') || (root.querySelector('#languageSelect')?.value) || 'en';
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
        parts.push(`<span class="text-blue-600 dark:text-blue-400">${start} → ${end}</span>`);
      }
      summaryEl.innerHTML = parts.join(' <span class="text-gray-400">•</span> ');
    }

    function hookThemeObserver(){
      try{
        const htmlEl = document.documentElement;
        const obs = new MutationObserver(() => {
          const dr = root.querySelector('#dateRange');
          const fp = dr && dr._flatpickr;
          if(fp && typeof fp.redraw === 'function'){
            fp.redraw();
          }
        });
        obs.observe(htmlEl, { attributes:true, attributeFilter:['class'] });
      }catch{}
    }
    const input = root.querySelector('#dateRange');
    const clearBtn = root.querySelector('#clearDateFilter');
    const langSelect = root.querySelector('#languageSelect');
    const orderSelect = root.querySelector('#orderSelect');

    console.log('initFromRoot - Found elements:', {
      input: !!input,
      clearBtn: !!clearBtn,
      langSelect: !!langSelect,
      orderSelect: !!orderSelect,
      rootId: root.id
    });

    const url = new URL(window.location.href);
    const existingStart = url.searchParams.get('startDate');
    const existingEnd = url.searchParams.get('endDate');
    const existingLang = url.searchParams.get('language') || 'en';
    const existingOrderBy = (url.searchParams.get('orderBy') || 'date').toLowerCase();
    const existingOrderDir = (url.searchParams.get('orderDir') || 'desc').toLowerCase();

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
    updateSummary();
    hookThemeObserver();

    let highlightDates = new Set();

    if (input && window.flatpickr){
      // Destroy existing flatpickr instance if it exists
      if (input._flatpickr) {
        console.log('Destroying existing flatpickr instance');
        try {
          input._flatpickr.destroy();
        } catch(e) {
          console.warn('Error destroying flatpickr:', e);
        }
      }

      console.log('Creating new flatpickr instance with dates:', existingStart, existingEnd);
      try {
        const fp = window.flatpickr(input, {
        mode: 'range',
        dateFormat: 'Y-m-d',
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
          highlightDates = await initMonthHighlights(fpInstance, (langSelect && langSelect.value) || existingLang);
          fpInstance.redraw();
        },
        onOpen: async function(selectedDates, dateStr, fpInstance){
          highlightDates = await initMonthHighlights(fpInstance, (langSelect && langSelect.value) || existingLang);
          fpInstance.redraw();
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
      });

        // Preload month highlights after init
        (async ()=>{
          highlightDates = await initMonthHighlights(fp, (langSelect && langSelect.value) || existingLang);
          fp.redraw();
        })();

        console.log('Flatpickr instance created successfully:', !!fp, 'on input:', input.id);

        // Verify the instance is actually attached
        setTimeout(() => {
          console.log('Flatpickr verification after 100ms:', {
            hasInstance: !!input._flatpickr,
            instanceType: typeof input._flatpickr,
            inputValue: input.value,
            selectedDates: input._flatpickr?.selectedDates?.length
          });
        }, 100);
      } catch(e) {
        console.error('Error creating flatpickr instance:', e);
      }
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

    langSelect && langSelect.addEventListener('change', function(){
      console.log('Language changed to:', langSelect.value);
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
  }

  // Expose Alpine component for categories panel
  window.blogCategories = function(){
    return { openCategories: false };
  };

  // Initialize on DOM ready
  document.addEventListener('DOMContentLoaded', function(){
    const root = document.querySelector('#content') || document;
    initFromRoot(root);
  });

  // Reinitialize after HTMX settles (DOM is ready for manipulation)
  document.body.addEventListener('htmx:afterSettle', function(evt){
    try{
      const target = evt && evt.detail && evt.detail.target;
      if (!target) {
        console.log('HTMX afterSettle - no target');
        return;
      }

      console.log('HTMX afterSettle - target:', target.id, target);

      // Check if this is the #content element or contains it
      if (target.id === 'content') {
        console.log('Re-initializing blog filters - target IS content');
        // Target IS the content div, use it directly
        // Give DOM a bit more time to fully settle before initializing flatpickr
        setTimeout(() => {
          console.log('Running initFromRoot after delay');
          initFromRoot(target);
        }, 50);
      } else if (target.querySelector?.('#content')) {
        console.log('Re-initializing blog filters - target CONTAINS content');
        // Target contains the content div, find it
        const contentDiv = target.querySelector('#content');
        setTimeout(() => {
          console.log('Running initFromRoot after delay');
          initFromRoot(contentDiv);
        }, 50);
      } else {
        console.log('Skipping reinitialization - not blog content');
      }
    }catch(err){
      console.error('Error reinitializing blog filters:', err);
    }
  });
})();
