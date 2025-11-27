const { Window } = require('happy-dom');
const path = require('path');

async function run(){
  const wnd = new Window();
  global.window = wnd.window;
  global.document = wnd.window.document;
  global.HTMLElement = wnd.window.HTMLElement;
  global.Event = wnd.window.Event;
  global.URL = wnd.window.URL;
  global.fetch = async (url) => ({ ok:true, json: async()=>({ dates:['2025-11-15','2025-12-05'] }) });

  // Mock flatpickr
  global.flatpickr = (input, opts) => {
    const fp = {
      currentYear: 2025,
      currentMonth: 10,
      selectedDates: [new Date('2025-11-01'), new Date('2025-11-30')],
      redraw: () => { console.log('redraw called'); },
      setDate: (d) => { console.log('setDate called with', d); fp.selectedDates = d; },
      destroy: () => {}
    };
    input._flatpickr = fp;
    return fp;
  };
  global.htmx = { ajax: () => {} };

  // Create DOM
  document.body.innerHTML = `
    <div id="content">
      <input id="dateRange" type="text" />
      <select id="languageSelect"><option value="en">English</option><option value="es">Spanish</option></select>
      <select id="orderSelect"><option value="date_desc">Newest</option></select>
      <button id="clearDateFilter"></button>
      <div class="post" data-published-date="2025-12-05"></div>
    </div>
  `;

  // Load the module
  const modulePath = path.resolve(__dirname, '..', 'src', 'js', 'blog-index.js');
  require(modulePath);

  // Init
  if (typeof window.blogIndexInit !== 'function'){
    console.error('blogIndexInit not found'); process.exit(2);
  }
  window.blogIndexInit(document.querySelector('#content'));

  // Simulate language change
  const lang = document.querySelector('#languageSelect');
  lang.value = 'es';
  lang.dispatchEvent(new Event('change'));

  await new Promise(r => setTimeout(r, 200));

  // Simulate htmx afterSettle
  document.body.dispatchEvent(new Event('htmx:afterSettle'));

  await new Promise(r => setTimeout(r, 300));

  console.log('Done');
}

run().catch(err => { console.error(err); process.exit(1); });

