# Using SweetAlert2 for HTMX Loading indicators (hx-indicator)
<datetime class="hidden">2025-04-21T20:05</datetime>
<!--category-- Javascript, HTMX -->


# Introduction
On a work project I've been using and abusing HTMX to build an admin UI. As part of this I'm using the lovely [SweetAlert2](https://sweetalert2.github.io/#examples)  Javascript library [for my confirmation dialogs](https://www.mostlylucid.net/blog/showingtoastandswappingwithhtmx#additional-htmx-features).  It works great but I also wanted to use them to replace my HTMX loading indicators.

This proved to be a CHALLENGE so I thought I'd document it here to save you the same pain. 

 <p style="color:red">Warning I'm a C# coder my Javascript is likely horrible.</p>

[TOC]
# The Problem
So HTMX is very clever, it's `hx-indicator` normally allows you to set a loading indicator for your HTMX requests.  Ordinarily this is an HTML element in your page like

```html

<div id="loading-modal" class="modal htmx-indicator">
    <div class="modal-box flex flex-col items-center justify-center bg-base-200 border border-base-300 shadow-xl rounded-xl text-base-content dark text-center ">
        <div class="flex flex-col items-center space-y-4">
            <h2 class="text-lg font-semibold tracking-wide">Loading...</h2>
            <span class="loading loading-dots loading-xl text-4xl text-stone-200"></span>
        </div>
    </div>
</div>
```

Then when you want to use it you'd decorate your HTMX request with `hx-indicator="#loading-modal"` and it will show the modal when the request is in progress ([see here for details](https://htmx.org/attributes/hx-indicator/)). 

Now HTMX does some clever magic using a `request` object [it tracks internally](https://github.com/bigskysoftware/htmx/blob/86893ebf4cb0f38484a522044f9a07cdd79398fa/src/htmx.js#L3293) 


```javascript
  function addRequestIndicatorClasses(elt) {
    let indicators = /** @type Element[] */ (findAttributeTargets(elt, 'hx-indicator'))
    if (indicators == null) {
      indicators = [elt]
    }
    forEach(indicators, function(ic) {
      const internalData = getInternalData(ic)
      internalData.requestCount = (internalData.requestCount || 0) + 1
      ic.classList.add.call(ic.classList, htmx.config.requestClass)
    })
    return indicators
  }

```

Wel lreplacing these therefore is a bit of a challenge. How do you track the requests and then show the SweetAlert2 modal when the request is in progress and hide it when it's finished.


# A Solution
So I set about (not because I had to, because I NEEDED to :)) to replace the HTMX loading indicator with a SweetAlert2 modal.
Anyway here's the code I came up with. 

You'd start by either importing SweetAlert2 in your HTML (as script & style tags) / import it for webpack or similar ([see their docs for this). 
](https://sweetalert2.github.io/#download)

After npm installing it you can import it like this in your JS file.
```javascript
import Swal from 'sweetalert2';
```

Then my main code looks like this:

```javascript
import Swal from 'sweetalert2';

const SWEETALERT_PATH_KEY = 'swal-active-path'; // Stores the path of the current SweetAlert
const SWEETALERT_HISTORY_RESTORED_KEY = 'swal-just-restored'; // Flag for navigation from browser history
const SWEETALERT_TIMEOUT_MS = 10000; // Timeout for automatically closing the loader

let swalTimeoutHandle = null;

export function registerSweetAlertHxIndicator() {
    document.body.addEventListener('htmx:configRequest', function (evt) {
        const trigger = evt.detail.elt;
        const indicatorAttrSource = getIndicatorSource(trigger);
        if (!indicatorAttrSource) return;

        const path = getRequestPath(evt.detail);
        if (!path) return;

        const currentPath = sessionStorage.getItem(SWEETALERT_PATH_KEY);

        // Show SweetAlert only if the current request path differs from the previous one
        if (currentPath !== path) {
            closeSweetAlertLoader();
            sessionStorage.setItem(SWEETALERT_PATH_KEY, path);
            evt.detail.indicator = null; // Disable HTMX's default indicator behavior

            Swal.fire({
                title: 'Loading...',
                allowOutsideClick: false,
                allowEscapeKey: false,
                showConfirmButton: false,
                theme: 'dark',
                didOpen: () => {
                    // Cancel immediately if restored from browser history
                    if (sessionStorage.getItem(SWEETALERT_HISTORY_RESTORED_KEY) === 'true') {
                        sessionStorage.removeItem(SWEETALERT_HISTORY_RESTORED_KEY);
                        Swal.close();
                        return;
                    }

                    Swal.showLoading();
                    document.dispatchEvent(new CustomEvent('sweetalert:opened'));

                    // Set timeout to auto-close if something hangs
                    clearTimeout(swalTimeoutHandle);
                    swalTimeoutHandle = setTimeout(() => {
                        if (Swal.isVisible()) {
                            console.warn('SweetAlert loading modal closed after timeout.');
                            closeSweetAlertLoader();
                        }
                    }, SWEETALERT_TIMEOUT_MS);
                },
                didClose: () => {
                    document.dispatchEvent(new CustomEvent('sweetalert:closed'));
                    sessionStorage.removeItem(SWEETALERT_PATH_KEY);
                    clearTimeout(swalTimeoutHandle);
                    swalTimeoutHandle = null;
                }
            });
        } else {
            // Suppress HTMX indicator if the path is already being handled
            evt.detail.indicator = null;
        }
    });

    //Add events to close the loader
    document.body.addEventListener('htmx:afterRequest', maybeClose);
    document.body.addEventListener('htmx:responseError', maybeClose);
    document.body.addEventListener('sweetalert:close', closeSweetAlertLoader);

    // Set a flag so we can suppress the loader immediately if navigating via browser history
    document.body.addEventListener('htmx:historyRestore', () => {
        sessionStorage.setItem(SWEETALERT_HISTORY_RESTORED_KEY, 'true');
    });

    window.addEventListener('popstate', () => {
        sessionStorage.setItem(SWEETALERT_HISTORY_RESTORED_KEY, 'true');
    });
}

// Returns the closest element with an indicator attribute
function getIndicatorSource(el) {
    return el.closest('[hx-indicator], [data-hx-indicator]');
}

// Determines the request path, including query string if appropriate
function getRequestPath(detail) {
    const responsePath =
        typeof detail?.pathInfo?.responsePath === 'string'
            ? detail.pathInfo.responsePath
            : (typeof detail?.pathInfo?.path === 'string'
                    ? detail.pathInfo.path
                    : (typeof detail?.path === 'string' ? detail.path : '')
            );

    const elt = detail.elt;

    // If not a form and has an hx-indicator, use the raw path
    if (elt.hasAttribute("hx-indicator") && elt.tagName !== "FORM") {
        return responsePath;
    }

    const isGet = (detail.verb ?? '').toUpperCase() === 'GET';
    const form = elt.closest('form');

    // Append query params for GET form submissions
    if (isGet && form) {
        const params = new URLSearchParams();

        for (const el of form.elements) {
            if (!el.name || el.disabled) continue;

            const type = el.type;
            if ((type === 'checkbox' || type === 'radio') && !el.checked) continue;
            if (type === 'submit') continue;

            params.append(el.name, el.value);
        }

        const queryString = params.toString();
        return queryString ? `${responsePath}?${queryString}` : responsePath;
    }

    return responsePath;
}

// Closes the SweetAlert loader if the path matches
function maybeClose(evt) {
    const activePath = sessionStorage.getItem(SWEETALERT_PATH_KEY);
    const path = getRequestPath(evt.detail);

    if (activePath && path && activePath === path) {
        closeSweetAlertLoader();
    }
}

// Close and clean up SweetAlert loader state
function closeSweetAlertLoader() {
    if (Swal.getPopup()) {
        Swal.close();
        document.dispatchEvent(new CustomEvent('sweetalert:closed'));
        sessionStorage.removeItem(SWEETALERT_PATH_KEY);
        clearTimeout(swalTimeoutHandle);
        swalTimeoutHandle = null;
    }
}
```

You configure this (if you're using ESM) in your `main.js` file like this

```javascript

import { registerSweetAlertHxIndicator } from './hx-sweetalert-indicator.js';
registerSweetAlertHxIndicator();
```

### Finding our elements
You'll see I use the `getIndicatorSource` function to find the element which triggered the HTMX request. This is important as we need to know which element triggered the request so we can close the modal when it's finished. This is important as HTMX has 'inheritance' so you need to climb ther the tree to find the element which triggered the request. 

```javascript
function getIndicatorSource(el) {
    return el.closest('[hx-indicator], [data-hx-indicator]');
}

```

Then on any HTMX request (so `hx-get` or `hx-post`) you can use the `hx-indicator` attribute to specify the SweetAlert2 modal. You don't even need to specify the class like before, just the parameter existing works. 


Let's go through how all this works:

##  Hooking it up with ` registerSweetAlertHxIndicator()` 
This acts as out entry point.  You can see it hooks into the `htmx:configRequest` event. This is fired when HTMX is about to make a request.

It then gets the element which triggered the event in `evt.detail.elt` and checks if it has an `hx-indicator` attribute. 

Finally, it shows the SweetAlert2 modal using `Swal.fire()`.

```javascript
rt function registerSweetAlertHxIndicator() {
    document.body.addEventListener('htmx:configRequest', function (evt) {
        const trigger = evt.detail.elt;
        const indicatorAttrSource = getIndicatorSource(trigger);
        if (!indicatorAttrSource) return;
        

```

## Getting the request path
If it does, it gets the request path using `getRequestPath(evt.detail)` and stores it in session storage.
Niw HTMX is a tricky bugger, it stores the path in different places depending on where you are in the lifecycle. So in my code I do ALL OF THE ABOVE. with `detail?.pathInfo?.path ?? detail?.path ?? '';`

It turns out that HTMX stored the *request* path in `detail.path` and the response path (for `document.body.addEventListener('htmx:afterRequest', maybeClose);
document.body.addEventListener('htmx:responseError', maybeClose);`) in `detail.PathInfo.responsePath` so we need too handle both. 

We also need to handle `GET` forms; as their response will likely include the URL elements passed in as `<input >` values so the response url can wind up being different. 


```javascript
// Returns the closest element with an indicator attribute
function getIndicatorSource(el) {
    return el.closest('[hx-indicator], [data-hx-indicator]');
}

// Determines the request path, including query string if appropriate
function getRequestPath(detail) {
    const responsePath =
        typeof detail?.pathInfo?.responsePath === 'string'
            ? detail.pathInfo.responsePath
            : (typeof detail?.pathInfo?.path === 'string'
                    ? detail.pathInfo.path
                    : (typeof detail?.path === 'string' ? detail.path : '')
            );

    const elt = detail.elt;

    // If not a form and has an hx-indicator, use the raw path
    if (elt.hasAttribute("hx-indicator") && elt.tagName !== "FORM") {
        return responsePath;
    }

    const isGet = (detail.verb ?? '').toUpperCase() === 'GET';
    const form = elt.closest('form');

    // Append query params for GET form submissions
    if (isGet && form) {
        const params = new URLSearchParams();

        for (const el of form.elements) {
            if (!el.name || el.disabled) continue;

            const type = el.type;
            if ((type === 'checkbox' || type === 'radio') && !el.checked) continue;
            if (type === 'submit') continue;

            params.append(el.name, el.value);
        }

        const queryString = params.toString();
        return queryString ? `${responsePath}?${queryString}` : responsePath;
    }

    return responsePath;
}
```
NOTE: This is especially the case if you use the `HX-Push-Url` header to change the URL of the request which HTMX stores for History.
### The Form
`HttpGet` forms are a little tricky so we have a piece of code which will detect if you've clicked a `submit` button inside a form and append the query string parameters caused by those pesky inputs to compare to the response URL. 

```javascript
const isGet = (detail.verb ?? '').toUpperCase() === 'GET';
    const form = elt.closest('form');

    // Append query params for GET form submissions
    if (isGet && form) {
        const params = new URLSearchParams();

        for (const el of form.elements) {
            if (!el.name || el.disabled) continue;

            const type = el.type;
            if ((type === 'checkbox' || type === 'radio') && !el.checked) continue;
            if (type === 'submit') continue;

            params.append(el.name, el.value);
        }

        const queryString = params.toString();
        return queryString ? `${responsePath}?${queryString}` : responsePath;
    }

    return responsePath;
    ```
    
This is important as HTMX will use the response URL to determine if the request is the same as the previous one. So we need to ensure we have the same URL in both places.

### Extensions
I use this little `Response` extension method to set the `HX-Push-Url` header in my ASP.NET Core app. I also added a second extension which will immediately close the modal (useful if you mess with the request and need to close it immediately). 
```csharp
public static class ResponseExtensions
{
    public static void PushUrl(this HttpResponse response, HttpRequest request)
    {
        response.Headers["HX-Push-Url"] = request.GetEncodedUrl();
    }
}
    public static void CloseSweetAlert(this HttpResponse response)
    {
        response.Headers.Append("HX-Trigger" , JsonSerializer.Serialize(new
        {
            sweetalert = "closed"
        }));

    }
}
```
This second one is handled here:

```javascript
    document.body.addEventListener('sweetalert:close', closeSweetAlertLoader);
```

## Storing the path
Ok so now we have the path, what do we do with it? Well to keep track of which request triggered the SweetAlert2 modal we store it in `sessionStorage` using `sessionStorage.setItem(SWEETALERT_PATH_KEY, path);`.

(Again you can make this more complex and ensure you only have one if you need.)

## Showing the modal
We then simply show the SweetAlert2 modal using `Swal.fire()`. note we have a bunch of options here.

On opening it checks for a session storage key `SWEETALERT_HISTORY_RESTORED_KEY` which is set when the history is restored. If it is, we close the modal immediately (it saves HTMX messing us up with it's odd history management).

We also fire an event `sweetalert:opened` which you can use to do any custom logic you need.

```javascript
Swal.fire({
    title: 'Loading...',
    allowOutsideClick: false,
    allowEscapeKey: false,
    showConfirmButton: false,
    theme: 'dark',
    didOpen: () => {
        // Cancel immediately if restored from browser history
        if (sessionStorage.getItem(SWEETALERT_HISTORY_RESTORED_KEY) === 'true') {
            sessionStorage.removeItem(SWEETALERT_HISTORY_RESTORED_KEY);
            Swal.close();
            return;
        }

        Swal.showLoading();
        document.dispatchEvent(new CustomEvent('sweetalert:opened'));

        // Set timeout to auto-close if something hangs
        clearTimeout(swalTimeoutHandle);
        swalTimeoutHandle = setTimeout(() => {
            if (Swal.isVisible()) {
                console.warn('SweetAlert loading modal closed after timeout.');
                closeSweetAlertLoader();
            }
        }, SWEETALERT_TIMEOUT_MS);
    },
    didClose: () => {
        document.dispatchEvent(new CustomEvent('sweetalert:closed'));
        sessionStorage.removeItem(SWEETALERT_PATH_KEY);
        clearTimeout(swalTimeoutHandle);
        swalTimeoutHandle = null;
    }
});
```

Additionally we set a timeout to handle cases where the request hangs. This is important as HTMX doesn't always close the modal if the request fails (especially if you use `hx-boost`). This is set here `const SWEETALERT_TIMEOUT_MS = 10000; // Timeout for automatically closing the loader` so we can close the modal if something goes wrong (it'll also log to the console).


## Closing it up
So now we have the modal open, we need to close it when the request is finished. To do this we call the `maybeClose` function. This is called when the request is finished (either successfully or with an error). 
Using `htmx:afterRequest` and `htmx:responseError` events.  These events fire once HTMX has finished a request (note, these are important, especialy for `HX-Boost` which can be a bit funny about what events it fires.)

```javascript
    document.body.addEventListener('htmx:afterRequest', maybeClose);
    document.body.addEventListener('htmx:responseError', maybeClose);
```

```javascript
    function maybeClose(evt) {
        const activePath = sessionStorage.getItem(SWEETALERT_PATH_KEY);
        const path = getRequestPath(evt.detail);

        if (activePath && path && activePath === path) {
            Swal.close();
            sessionStorage.removeItem(SWEETALERT_PATH_KEY);
        }
    }
```

You'll see this function checks if the path in session storage is the same as the path of the request. If it is, it closes the modal and removes the path from session storage.

## It's History
HTMX has a fiddly way of handling history which could leave the modal 'stuck' open on doing a back page. So we add a couple more events to catch this  (most of the time we only need one but belt & braces).

```javascript
    //Add events to close the loader
    document.body.addEventListener('htmx:afterRequest', maybeClose);
    document.body.addEventListener('htmx:responseError', maybeClose);
    document.body.addEventListener('sweetalert:close', closeSweetAlertLoader);

    // Set a flag so we can suppress the loader immediately if navigating via browser history
    document.body.addEventListener('htmx:historyRestore', () => {
        sessionStorage.setItem(SWEETALERT_HISTORY_RESTORED_KEY, 'true');
    });

    window.addEventListener('popstate', () => {
        sessionStorage.setItem(SWEETALERT_HISTORY_RESTORED_KEY, 'true');
    });`
```
You'll see we also set the `sessionStorage.setItem(SWEETALERT_HISTORY_RESTORED_KEY, 'true');` which we check for in the `didOpen` event:

```javascript
           didOpen: () => {
    // Cancel immediately if restored from browser history
    if (sessionStorage.getItem(SWEETALERT_HISTORY_RESTORED_KEY) === 'true') {
        sessionStorage.removeItem(SWEETALERT_HISTORY_RESTORED_KEY);
        Swal.close();
        return;
    }
```
We do it in the event as the modal doesn't alwasy open immediately on `popstate` \ `htmx:historyRestore`  (especially if you have a lot of history). So we need to check for it in the `didOpen` event (hence it being in session key, sometimes this can reload etc...so we have to be aware of that).

# BONUS - For my PagingTagHelper
If you're using my [PagingTagHelper](https://www.mostlylucid.net/blog/category/PagingTagHelper) there's an issue with the `PageSize` and using SweetAlert; this is due to it ALSO hooking the ` 
`htmx:configRequest` event. So we need to add a little code to ensure it doesn't conflict with the SweetAlert2 modal.

```javascript
(() => {
    if (window.__pageSizeListenerAdded) return;

    document.addEventListener('htmx:configRequest', event => {
        const { elt } = event.detail;
        if (elt?.matches('[name="pageSize"]')) {
            const params = new URLSearchParams(window.location.search);
            params.set('pageSize', elt.value);

            const paramObj = Object.fromEntries(params.entries());
            event.detail.parameters = paramObj;
            
            const pageSizeEvent = new CustomEvent('pagesize:updated', {
                detail: {
                    params: paramObj,
                    elt,
                },
            });

            document.dispatchEvent(pageSizeEvent);
        }
    });

    window.__pageSizeListenerAdded = true;
})();
```

To fix this you need to add a little function:

```javascript
/**
 * Appends the current pageSize from the browser's URL to the HTMX request path,
 * modifying event.detail.path directly.
 *
 * @param {CustomEvent['detail']} detail - The HTMX event.detail object
 */
export function getPathWithPageSize(detail) {
    const originalPath = detail.path;
    if (!originalPath) return null;

    const selectedValue = detail.elt?.value ?? '';
    const url = new URL(originalPath, window.location.origin);

    // Use the current location's query string, update pageSize
    const locationParams = new URLSearchParams(window.location.search);
    locationParams.set('pageSize', selectedValue);
    url.search = locationParams.toString();

    return url.pathname + url.search;
}
```
This just gets the request and adds the `pagesize` parameter to it. Then import this function

```javascript
import { getPathWithPageSize } from './pagesize-sweetalert'

export function registerSweetAlertHxIndicator() {
    document.body.addEventListener('htmx:configRequest', function (evt) {
        const trigger = evt.detail.elt;

        const indicatorAttrSource = getIndicatorSource(trigger);
        if (!indicatorAttrSource) return;

        // ✅ If this is a pageSize-triggered request, use our custom path
        let path;
        if (evt.detail.headers?.['HX-Trigger-Name'] === 'pageSize') {
            path = getPathWithPageSize(evt.detail);
            console.debug('[SweetAlert] Using custom path with updated pageSize:', path);
        } else {
            path = getRequestPath(evt.detail);
        }
        ...rest of code

```
You can see here that we detect the `pageSize` trigger and when it finds it (so you changed pagesize) it uses the `getPathWithPageSize` function to get the new path. The `pageSize` code will replace this later in the pipeline but this lets it detect correctly so it closes as expected. 


# In Conclusion
So that's how you can use SweetAlert2 as an HTMX loading indicator. It's a bit of a hack but it works and it's a nice way to use the same library for both loading indicators and confirmation dialogs.9