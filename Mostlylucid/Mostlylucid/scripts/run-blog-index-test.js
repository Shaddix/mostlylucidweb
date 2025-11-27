// ...existing code...
const { Window } = require('happy-dom');
const path = require('path');

async function run(){
  const wnd = new Window();
  global.window = wnd.window;
  global.document = wnd.window.document;
  global.HTMLElement = wnd.window.HTMLElement;
  global.Event = wnd.window.Event;
  global.URL = wnd.window.URL;

  let redrawCalled = false;
  let setDateCalled = false;
  let fetchCalled = false;

  global.fetch = async (url) => { fetchCalled = true; return { ok:true, json: async()=>({ dates:['2025-11-15','2025-12-05'] }) }; };

  // Mock flatpickr
  global.flatpickr = (input, opts) => {
    const fp = {
      currentYear: 2025,
      currentMonth: 10,
      selectedDates: [new Date('2025-11-01'), new Date('2025-11-30')],
      redraw: () => { redrawCalled = true; console.log('redraw called'); },
      setDate: (d) => { setDateCalled = true; console.log('setDate called with', d); fp.selectedDates = d; },
      destroy: () => {}
    };
    input._flatpickr = fp;
    if (opts && opts.defaultDate) {
      fp.selectedDates = opts.defaultDate.map(d => (typeof d === 'string' ? new Date(d + 'T00:00:00') : d));
    }
    console.log('flatpickr created');
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
  console.log('Requiring', modulePath);
  require(modulePath);
  console.log('Required module');

  // Init
  if (typeof window.blogIndexInit !== 'function'){
    console.error('blogIndexInit not found'); process.exit(2);
  }
  window.blogIndexInit(document.querySelector('#content'));
  console.log('Called blogIndexInit');

  // Simulate language change
  const lang = document.querySelector('#languageSelect');
  lang.value = 'es';
  lang.dispatchEvent(new Event('change'));
  console.log('Dispatched language change');

  await new Promise(r => setTimeout(r, 300));

  // Simulate htmx afterSettle
  document.body.dispatchEvent(new Event('htmx:afterSettle'));
  console.log('Dispatched htmx afterSettle');

  await new Promise(r => setTimeout(r, 400));

  console.log('fetchCalled:', fetchCalled, 'redrawCalled:', redrawCalled, 'setDateCalled:', setDateCalled);

  if(fetchCalled && redrawCalled && setDateCalled){
    console.log('TEST PASS');
    process.exit(0);
  } else {
    console.error('TEST FAIL');
    process.exit(1);
  }
}

run().catch(err => { console.error(err); process.exit(1); });
// ...existing code...
