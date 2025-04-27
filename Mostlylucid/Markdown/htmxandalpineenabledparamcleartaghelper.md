 # An Alpine.js and HTMX 'Clear Query String Parameter(s)'  tag helper for ASP.NET Core
<datetime class="hidden">2025-04-25T23:00</datetime>
<!--category-- Javascript, HTMX, Alpine.js, ASP.NET Core -->

# Introduction
Just a quick one, I had a need in a work project for the ability to 'clear' URL parameters from a URL. This is useful when you have a URL with multiple parameters, and you want to remove one or more of them (for example for a search filter). 

[TOC]

# The Problem

My current project uses old-school query strings (it's an admin site so doesn't need the fanciness of 'nice' urls). So I wind up with a URL like this:

```http
/products?category=electronics&search=wireless+headphones&sort=price_desc&inStock=true&page=3

```

Now these can vary with each page, so I can end up with a BUNCH in the page URL and I need to be able to clear them out without writing a bunch of boilerplate to do it.

You CAN do this as part of whatever input control you use so for instance next to each checkbox (or a fancy placeholder style clear icon) but and you can use this technique for those too. However, in this case I wanted to do two main things:
1. Be able to clear a named parameter
2. Be able to clear a list of parameters.
3. Be able to clear all parameters
4. Have it post back with HTMX 
5. Have it use my loading indicator [my loading indicator](https://www.mostlylucid.net/blog/usingsweetalertforhxindicators). 


# The Solution
In my project I already use
- HTMX
- Alpine.js
- ASP.NET Core 
- TailwindCSS
- DaisyUI

So my solution was focused around using these to get a nice looking, functional solution with minimal code. 

## The Tag Helper
My TagHelper is pretty simple, all I do is create an `<a>` tag with a few attributes I'll later pass into the Alpine Module and we're done. 

```csharp
[HtmlTargetElement("clear-param")]
public class ClearParamTagHelper : TagHelper
{
    [HtmlAttributeName("name")]
    public string Name { get; set; }
    
    [HtmlAttributeName("all")]
    public bool All { get; set; }= false;
    
    [HtmlAttributeName("target")]
    public string Target { get; set; } = "#page-content";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "a";
        output.Attributes.SetAttribute("x-data", "window.queryParamClearer({})");

        if (All)
        {
        output.Attributes.SetAttribute("x-all", All);
        }
        else
        {
            output.Attributes.SetAttribute("x-param", Name);
        }
        output.Attributes.SetAttribute("data-target", Target);
        output.Attributes.SetAttribute("x-on:click.prevent", "clearParam($event)");
        output.Content.SetHtmlContent(@"
            <div class='w-6 h-6 flex items-center justify-center bg-red-600 text-white rounded-full'>
                <i class='bx bx-x text-lg'></i>
            </div>");
    }
}
```

### Parameters

In use this looks like this, first for 'clear all parameters'. So I just look at the `Context.Request.Query` if there's any parameters there I render the little `x` icon to let the user clear all the parameters.

```html

@if(Context.Request.Query.Any())
{
<label class="param-label">
    <clear-param all="true"></clear-param>
    clear all
</label>
}
</div>

```

Alternatively for named parameters I can do this


```html

<div class="param-label">
    <clear-param name="myParam"></clear-param>
    <p>My Param: @Model.MyParam</p>
</div>
```



Which would of course clear that single parameter. 

Or even

```html

<div class="param-label">
    <clear-param name="myParam1,myParam2,myParam3"></clear-param>
    <p>My Param: @Model.MyParam1</p>
    <p>My Param: @Model.MyParam2</p>
    <p>My Param: @Model.MyParam3</p>
</div>
```

This then clears all the named parameters from the string. 

### The `target` attribute
YOu also have the option to pass in a `target` attribute which will be used as the `hx-target` attribute. This is useful if you want to update a specific part of the page with the new content. 

```html

<div class="param-label">
    <clear-param name="myParam" target="#my-thing"></clear-param>
    <p>My Param: @Model.MyParam</p>
</div>
```

In my case (because I wroted it) I defaulted the target to my `#page-content` div.

```csharp
    [HtmlAttributeName("target")]
    public string Target { get; set; } = "#page-content";

```


### The Result

These result in the rendering of the following HTML:

- All:
So we get HTML with the `x-all` attribute and no `x-param` attribute. 

```html
<a x-data="window.queryParamClearer({})" x-all="True" data-target="#page-content" x-on:click.prevent="clearParam($event)">
    <div class="w-6 h-6 flex items-center justify-center bg-red-600 text-white rounded-full">
        <i class="bx bx-x text-lg"></i>
    </div>
</a>

```
- Single
We get HTML with the `x-param` attribute and no `x-all` attribute. 

```html
<a x-data="window.queryParamClearer({})" x-param="myParam" data-target="#page-content" x-on:click.prevent="clearParam($event)">
    <div class="w-6 h-6 flex items-center justify-center bg-red-600 text-white rounded-full">
        <i class="bx bx-x text-lg"></i>
    </div>
</a>
```

- Multiple 
We get HTML with the `x-param` attribute with a comma separated string and no `x-all` attribute. 

```html
<a x-data="window.queryParamClearer({})" x-param="myParam1,myParam2,myParam3" data-target="#page-content" x-on:click.prevent="clearParam($event)">
    <div class="w-6 h-6 flex items-center justify-center bg-red-600 text-white rounded-full">
        <i class="bx bx-x text-lg"></i>
    </div>
</a>
```

Each of them also has the two Alpine attributes `x-data` and `x-on:click.prevent` which are used to set up the Alpine module and call the function to clear the parameters.

We'll see how that works next...

## The Alpine Module
This is of course made possible through the use of Alpine.js to configure our request and HTMX to perform it. 

As you can see in the code below, I have a simple module that takes the `path` of the current page and then uses the `URL` API to parse the query string (you could also pass in a different for whatever reason :)) .

We then get the element which was clicked and check if it has the `x-all` attribute; if it does we delete all the parameters from the URL, otherwise we split the `x-param` attribute by commas and delete each of those parameters. 

Then we create a new URL with the updated query string and use HTMX to make a request to that URL. 


```javascript
export function queryParamClearer({ path = window.location.pathname }) {
    return {
        clearParam(e) {
            const el = e.target.closest('[x-param],[x-all]');
            if (!el) return;

            const url = new URL(window.location.href);

            if (el.hasAttribute('x-all')) {
                // → delete every single param
                // we copy the keys first because deleting while iterating modifies the collection
                Array.from(url.searchParams.keys())
                    .forEach(key => url.searchParams.delete(key));
            } else {
                // → delete only the named params
                (el.getAttribute('x-param') || '')
                    .split(',')
                    .map(p => p.trim())
                    .filter(Boolean)
                    .forEach(key => url.searchParams.delete(key));
            }

            const qs = url.searchParams.toString();
            const newUrl = path + (qs ? `?${qs}` : '');

            showAlert(newUrl);
            htmx.ajax('GET', newUrl, {
                target: el.dataset.target || el.getAttribute('hx-target') || 'body',
                swap: 'innerHTML',
                pushUrl: true
            });
        }
    };
}

//In your entry point / anywhere you want to register the module
import { queryParamClearer } from './param-clearer.js'; // webpackInclude: true

window.queryParamClearer = queryParamClearer;
```

### The `showAlert` function using SweetAlert2

You'll also note that I call a `showAlert` function. This is just a simple wrapper around the SweetAlert2 loading indicator I use in my project. You can of course replace this with whatever you want to do.' 

This is slightly tweaked from the [last time we saw it](https://www.mostlylucid.net/blog/usingsweetalertforhxindicators). So I could extract the `showAlert` function and make it available to external modules. Which let's me use it in both the `param-clearer` module and the `hx-indicator` module. 

```javascript
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

        if (!path) return;
        evt.detail.indicator = null;
        showAlert(path);
    });
}

export function showAlert(path)
{
    const currentPath = sessionStorage.getItem(SWEETALERT_PATH_KEY);

    // Show SweetAlert only if the current request path differs from the previous one
    if (currentPath !== path) {
        closeSweetAlertLoader();
        sessionStorage.setItem(SWEETALERT_PATH_KEY, path);


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
    }
}

//Register it
import { registerSweetAlertHxIndicator, showAlert } from './hx-sweetalert-indicator.js';
registerSweetAlertHxIndicator();
window.showAlert = showAlert;
```

As a reminder this uses the `path` as the key to know when to hide the alert. 


### HTMX
Finally, we use `htmx.ajax` to make the request. This is a simple GET request to the new URL we created with the updated query string.

```javascript
   htmx.ajax('GET', newUrl, {
                target: el.dataset.target || el.getAttribute('hx-target') || 'body',
                swap: 'innerHTML',
                pushUrl: true
            });
```

# In Conclusion
This is a simple way to clear URL parameters using a tag helper and Alpine.js. It allows you to clear all parameters, or just specific ones, with minimal code.