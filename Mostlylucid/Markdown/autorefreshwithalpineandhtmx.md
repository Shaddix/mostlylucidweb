# An Auto-Update Partial Updater with Alpine.js and HTMX

<datetime class="hidden">2025-04-23T19:30</datetime>
<!--category-- Javascript, HTMX, Alpine.js, ASP.NET Core -->


# Introduction
So in what's becoming a series, in a work project I wanted to add the ability for a partial to auto-update on a given timescale. 

Here's how I did it using Alpine.js and HTMX.

[TOC]



# Requirements
So I wanted this to be
1. reusable; so it should be simple and self-contained enough to auto-update any element.
2. It should respect existing Url parameters
3. It should be detectable Server Side (in ASP.NET Core in this instance)
4. If turned on it should be enabled *for that endpoint* only and this should be remembered between requests.
5. It should instantly do the update when enabled (so the user knows what it looks like)
6. It should be able to be turned off by the user
7. It should be simple to include in a page. 

With this in mind I set out to build a small JS module using Alpine.js and HTMX.

## NOTE
You can do auto updates without the 'on and off' and 'remember' features pretty simply with HTMX alone.
For example; using [HTMX Triggers ](https://htmx.org/attributes/hx-trigger/) you can really do a lot of stuff. 

```html

<div id="campaignemail-request-list" hx-get="@Url.Action("List", "CampaignEmailRequest")" hx-trigger="every 30s" hx-swap="innerHTML">
    <partial name="_List" model="@Model"/>
</div>
```
Thanks to [@KhalidAbuhakmeh](https://khalidabuhakmeh.com/) for pointing this out.

This code does in fact use `hx-trigger` to set up the auto-update. Just using Alpine.js to configure the HTMX attributes. 
It's why HTMX with Alpine.js is such a powerful combination; HTMX handles all the server interaction and the AJAX requests, while Alpine.js handles the client side state and interaction. Could you also do this in Vanilla JS? Sure, but you wind up writing a bunch of code to do the same thing these two TINY libraries already do.


# The Code

The code for this is really pretty compact, it's broken up into two main parts; a JS module, the event handlers and the HTML.

## The Module

The module is a simple JS module that uses Alpine.js to manage the state of the auto-update. It uses local storage to remember the state of the auto-update between requests. 


It accepts the params :
- `endpointId` - the id of the element to be updated
- `actionUrl` - the url to be called to update the element
- `initialInterval` - the initial interval to be used for the auto-update (default is 30 seconds)

We can also see it uses a couple of keys; these are used for local storage to remember the state of the auto-update. 
You can see that I use the `actionurl` as part of the key to make this endpoint specific. 

```javascript
export function autoUpdateController(endpointId, actionUrl, initialInterval = 30) {
const keyPrefix = `autoUpdate:${actionUrl}`;
const enabledKey = `${keyPrefix}:enabled`;

    return {
        autoUpdate: false,
        interval: initialInterval,

        toggleAutoUpdate() {
            const el = document.getElementById(endpointId);
            if (!el) return;

            const url = new URL(window.location.href);
            const query = url.searchParams.toString();
            const fullUrl = query ? `${actionUrl}?${query}` : actionUrl;

            const wasPreviouslyEnabled = localStorage.getItem(enabledKey) === 'true';

            if (this.autoUpdate) {
                el.setAttribute('hx-trigger', `every ${this.interval}s`);
                el.setAttribute('hx-swap', 'innerHTML');
                el.setAttribute('hx-get', fullUrl);
                el.setAttribute('hx-headers', JSON.stringify({ AutoPoll: 'auto' }));

                localStorage.setItem(enabledKey, 'true');

                htmx.process(el); // rebind with updated attributes
                
                if (!wasPreviouslyEnabled) {
                    htmx.ajax('GET', fullUrl, {
                        target: el,
                        swap: 'innerHTML',
                        headers: {AutoPoll: 'auto'}
                    });
                }
            } else {
                el.removeAttribute('hx-trigger');
                el.removeAttribute('hx-get');
                el.removeAttribute('hx-swap');
                el.removeAttribute('hx-headers');

                localStorage.removeItem(enabledKey);
                htmx.process(el);
            }
        },

        init() {
            this.autoUpdate = localStorage.getItem(enabledKey) === 'true';
            this.toggleAutoUpdate();
        }
    };
}
```

### `toggleAutoUpdate()` Method

This method enables or disables automatic polling of a target HTML element using HTMX.

### Behavior

- Selects an element by its `endpointId`.
- Builds the request URL (`fullUrl`) by combining the given `actionUrl` with the current page's query string.
- Checks if polling was previously enabled by reading from `localStorage` (good as it is remembered between browser sessions).

#### When `this.autoUpdate` is `true` (i.e., polling is enabled):
- Sets HTMX attributes on the element:
    - `hx-trigger` to poll every `interval` seconds
    - `hx-swap="innerHTML"` to replace the element’s content
    - `hx-get` to point to the polling URL
    - `hx-headers` to add a custom `"AutoPoll": "auto"` header
- Saves the enabled state to `localStorage`
- Calls `htmx.process(el)` to let HTMX recognize the new attributes
- If it was previously off, immediately triggers an HTMX request via `htmx.ajax()` (not relying on HTMX event wiring)

#### When `this.autoUpdate` is `false` (i.e., polling is disabled):
- Removes the above HTMX attributes
- Clears the saved setting from `localStorage`
- Calls `htmx.process(el)` again to update HTMX behavior

### Auto Poll when first enabled
We also have a branch in here to perform the auto-poll when first enabled. 

```javascript
const wasPreviouslyEnabled = localStorage.getItem(enabledKey) === 'true';
      if (!wasPreviouslyEnabled) {
                    htmx.ajax('GET', fullUrl, {
                        target: el,
                        swap: 'innerHTML',
                        headers: {AutoPoll: 'auto'}
                    });
                }
```

This performs an HTMX request to the `fullUrl` and updates the target element with the response. This is useful for showing the user what the auto-update will look like when they enable it.

## Headers
You'll note we also send an HTMX header with the request. This is important as it allows us to detect the request server side. 

```javascript
   el.setAttribute('hx-headers', JSON.stringify({ AutoPoll: 'auto' }));
headers: {AutoPoll: 'auto'}

```

In my server side I then detect this header being set using

```csharp
 if (Request.Headers.TryGetValue("AutoPoll", out _))
        {
            
            
            var utcDate = DateTime.UtcNow;
            var parisTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");
            var parisTime = TimeZoneInfo.ConvertTimeFromUtc(utcDate, parisTz);

            var timeStr = parisTime.ToString("yyyy-MM-dd HH:mm:ss");
              Response.ShowToast($"Auto Update Last updated: {timeStr} (paris)",true); 
         
            return PartialView("_List", requests);
        }
```

You'll see I just look forthe header with `Request.Headers.TryGetValue("AutoPoll", out _)` and if it's there I know it's an auto-poll request.

I then grab the current yime (it's for a French customer, so I convert to Paris time) and show a toast with the time.

### ShowToast

The `ShowToast` method is a simple extension method that sets a trigger to tell HTMX to show a toast message. 

```csharp
    public static void ShowToast(this HttpResponse response, string message, bool success = true)
    {
        response.Headers.Append("HX-Trigger", JsonSerializer.Serialize(new
        {
            showToast = new
            {
                toast = message,
                issuccess =success
            }
        }));

    }
```

This is then detected by my HTMX toast component which shows the message. 

```javascript
document.body.addEventListener("showToast", (event) => {
    const { toast, issuccess } = event.detail || {};
    const type = issuccess === false ? 'error' : 'success';
    showToast(toast || 'Done!', 3000, type);
});

```

This then calls into my Toast component I [wrote about here ](https://www.mostlylucid.net/blog/showingtoastandswappingwithhtmx).

### Hooking it up
It's pretty simple to hook this module up, in your main.js \ index.js whatever just import it and hook it up to Window
```javascript
import './auto-actions';

window.autoUpdateController = autoUpdateController; //note this isn't strictly necessary but it makes it easier to use in the HTML


//Where we actually hook it up to Alpine.js
document.addEventListener('alpine:init', () => {
    Alpine.data('autoUpdate', function () {
        const endpointId = this.$el.dataset.endpointId;
        const actionUrl = this.$el.dataset.actionUrl;
        const interval = parseInt(this.$el.dataset.interval || '30', 10); // default to 30s

        return autoUpdateController(endpointId, actionUrl, interval);
    });
});

```

We then call the init method in the ASP.NET Razor code:


## ASP.NET Razor Code

To make this as small and reusable as possible the Razor code is pretty simple. 

Here you can see I specify the Alpine.js data attributes to set up the auto-update. 

- x-data: This is where we set up the Alpine.js data object.
- x-init: This is where we call the init method on the auto-update controller.
- x-on:change: This is where we call the toggleAutoUpdate method on the auto-update controller.
- data-endpoint-id: This is the id of the element to be updated.
- data-action-url: This is the url to be called to update the element.
- data-interval: This is the interval to be used for the auto-update (default is 30 seconds).


You'll see we set the target to use for the request to the `campaignemail-request-list` element. This is the element that will be updated with the new content.
 That's then included SOMEWHERE in the page.

Now when a checkbox is checked it will automatically update the list every 30 seconds. 

```html
            <div class=" px-4 py-2 bg-base-100 border border-base-300 rounded-box"
                x-data="autoUpdate()" 
                x-init="init"
                x-on:change="toggleAutoUpdate"
                data-endpoint-id="campaignemail-request-list"
                data-action-url="@Url.Action("List", "CampaignEmailRequest")"
                data-interval="30"
                >
                <label class="flex items-center gap-2">
                    <input type="checkbox" x-model="autoUpdate" class="toggle toggle-sm" />
                    <span class="label-text">
                        Auto update every <span x-text="$data.interval"></span>s
                    </span>
                </label>
            </div>

        <!-- Voucher List -->
        <div id="campaignemail-request-container">
            <div
                id="campaignemail-request-list">
                <partial name="_List" model="@Model"/>
            </div>
        </div>
```

# In Conclusion
And that's it, pretty simple right. Leveraging HTMX and Alpine.js to create a simple auto-update component we can use easily from ASP.NET Core. 