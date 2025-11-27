# Showing Toast and Swapping Content with HTMX (And ASP.NET Core)
<datetime class="hidden">2025-04-12T13:33</datetime>
<!--category-- ASP.NET Core, HTMX -->

# Introduction
HTMX is a great library for making your web applications more dynamic and responsive. In this post, I'll show you how to use HTMX to show a toast notification and swap content on the page.

One of the 'limitations' in standard HTMX (i.e., [not OOB swaps](https://htmx.org/attributes/hx-swap-oob/))  is that you usually only have a single piece of content swapped from the back end. However this can be overcome with the use of `HX-Trigger` headers and a little javascript.

**NOTE: You CAN use [`hx-swap-oob`](https://htmx.org/attributes/hx-swap-oob/) to swap two different *content* elements but this is a bit more complex and not as easy to use for doing some JavaScripty stuff.**

[TOC]

# TOAST
I've been using this a variant of this simple toast notification system [for a while now](https://www.mostlylucid.net/blog/acopybuttonforhightlightjs#showtoast-function). It's a simple function that takes a message, duration, and type (success, error, warning) and shows a toast notification on the page.

This 'latest version' has some more bling around icons, animations etc...

## The Javascript

```javascript
// HTMX toast notification
// Simple HTMX toast handler for use with hx-on::after-request
window.showToast = (message, duration = 3000, type = 'info') => {
    const toast = document.getElementById('toast');
    const toastMessage = document.getElementById('toast-message');
    const toastText = document.getElementById('toast-text');
    const toastIcon = document.getElementById('toast-icon');

    // Reset classes
    toastMessage.className = 'alert shadow-lg gap-2 transition-all duration-300 ease-in-out cursor-pointer';
    toastIcon.className = 'bx text-2xl';

    // Add DaisyUI alert type
    const alertClass = `alert-${type}`;
    toastMessage.classList.add(alertClass);

    // Add icon class
    const iconMap = {
        success: 'bx-check-circle',
        error: 'bx-error-circle',
        warning: 'bx-error',
        info: 'bx-info-circle'
    };
    const iconClass = iconMap[type] || 'bx-bell';
    toastIcon.classList.add(iconClass);

    // Set the message
    toastText.textContent = message;

    // Add slide-in animation
    toastMessage.classList.add('animate-slide-in');
    toast.classList.remove('hidden');

    // Allow click to dismiss
    toastMessage.onclick = () => hideToast();

    // Auto-dismiss
    clearTimeout(window.toastTimeout);
    window.toastTimeout = setTimeout(() => hideToast(), duration);

    function hideToast() {
        toastMessage.classList.remove('animate-slide-in');
        toastMessage.classList.add('animate-fade-out');
        toastMessage.onclick = null;

        toastMessage.addEventListener('animationend', () => {
            toast.classList.add('hidden');
            toastMessage.classList.remove('animate-fade-out');
        }, { once: true });
    }
};

```

This uses a little HTML snippet I define in my _Layout.cshtml file (using my preferred Tailwind CSS & DaisyUI). Note the 'class preserving block' at the end. This is a little trick to ensure that the classes are preserved in the final HTML output. This is really for my tailwind setup as I only look at `cshtml`.


```html
<div
        id="toast"
        class="toast toast-bottom fixed z-50 hidden w-full md:w-auto max-w-sm right-4 bottom-4"
>
    <div
            id="toast-message"
            class="alert shadow-lg gap-2 transition-all duration-300 ease-in-out cursor-pointer"
    >
        <i id="toast-icon" class="bx text-2xl"></i>
        <span id="toast-text">Notification message</span>
    </div>
</div>

<!-- class-preserving dummy block -->
<div class="hidden">
    <div class="alert alert-success alert-error alert-warning alert-info"></div>
    <i class="bx bx-check-circle bx-error-circle bx-error bx-info-circle bx-bell"></i>
    <div class="animate-slide-in animate-fade-out"></div>
</div>
```

## Tailwind
Here I define what files to 'tree-shake' from as well as define some animation classes the toast uses. 


```javascript
const defaultTheme = require("tailwindcss/defaultTheme");

module.exports = {
  content: ["./Views/**/*.cshtml", "./Areas/**/*.cshtml"],
  safelist: ["dark"],
  darkMode: "class",
  theme: {
    extend: {
      keyframes: {
        'slide-in': {
          '0%': { opacity: 0, transform: 'translateY(20px)' },
          '100%': { opacity: 1, transform: 'translateY(0)' },
        },
        'fade-out': {
          '0%': { opacity: 1 },
          '100%': { opacity: 0 },
        },
      },
      animation: {
        'slide-in': 'slide-in 0.3s ease-out',
        'fade-out': 'fade-out 0.5s ease-in forwards',
      },
  },
  plugins: [require("daisyui")],
};
```

# Triggered
The secret of making this all work is using the [HTMX Trigger header functionality](https://htmx.org/headers/hx-trigger/). 

Now 'normally' you would [define this in your actual html / razor code](https://htmx.org/attributes/hx-trigger/):

```html
<div hx-get="/clicked" hx-trigger="click[ctrlKey]">Control Click Me</div>
```

Or you can define it in an after request event. So you do something then it triggers a new event. 

```html
<button 
    hx-get="/api/do-something"
    hx-swap="none"
    hx-on::afterRequest="window.showToast('API call complete!', 3000, 'success')"
    class="btn btn-primary"
>
    Do Something
</button>
```
This is handy if you just want to 'do something then indicate it's done' but in my case I want to swap some content AND show a toast.


```csharp
            Response.Headers.Append("HX-Trigger", JsonSerializer.Serialize(new
            {
                showToast = new
                {
                    toast = result.Message,
                    issuccess = result.Success
                }
            }));
```

In my case my trigger is named `showToast` and I pass in a message and a success flag. So i my JS I have defined an event listener for this event.  This then calls into the `showToast` function and passes in the message and success flag.

```javascript
// Handles HX-Trigger: { "showToast": { "toast": "...", "issuccess": true } }
document.body.addEventListener("showToast", (event) => {
    const { toast, issuccess } = event.detail || {};
    const type = issuccess === false ? 'error' : 'success';
    showToast(toast || 'Done!', 3000, type);
});
```

# ASP.NET
So  why  do I use this? Well in a recent work project I wanted to take some action on a user displayed in a table. I wanted to show a toast notification and swap the content of the user row with the new content.

![userrow.png](userrow.png)

As you can see I have a BUNCH of buttons which 'do stuff' to the user. I wanted to show a toast notification and swap the content of the user row with the new content.

So in my controlled I have a simple 'switch' which takes the action name, does stuff then returns the new request result.

```csharp
    private async Task ApplyAction(string email, string useraction)
    {
        if (!string.IsNullOrWhiteSpace(useraction) &&
            Enum.TryParse<UserActionType>(useraction, true, out var parsedAction))
        {
            RequestResult result;

            switch (parsedAction)
            {
                case UserActionType.FlipRoles:
                    result = await userActionService.FlipRestaurantPermissions(email);
                    break;
                case UserActionType.UnflipRoles:
                    result = await userActionService.UnFlipRestaurantPermissions(email);
                    break;
                case UserActionType.Enable2FA:
                    result = await userActionService.ToggleMFA(email, true);
                    break;
                case UserActionType.Disable2FA:
                    result = await userActionService.ToggleMFA(email, false);
                    break;~~~~
                case UserActionType.RevokeTokens:
                    result = await userActionService.RevokeTokens(email);
                    break;
                case UserActionType.Lock:
                    result = await userActionService.Lock(email);
                    break;
                case UserActionType.Unlock:
                    result = await userActionService.Unlock(email);
                    break;
                case UserActionType.Nuke:
                    result = await userActionService.Nuke(email);
                    break;
                case UserActionType.Disable:
                    result = await userActionService.DisableUser(email);
                    break;
                case UserActionType.Enable:
                    result = await userActionService.EnableUser(email);
                    break;
                case UserActionType.ResetPassword:
                    result = await userActionService.ChangePassword(email);
                    break;
                case UserActionType.SendResetEmail:
                    result = await userActionService.SendResetEmail(email);
                    break;
                default:
                    result = new RequestResult(false, "Unknown action");
                    break;
                  
            }

            Response.Headers.Append("HX-Trigger", JsonSerializer.Serialize(new
            {
                showToast = new
                {
                    toast = result.Message,
                    issuccess = result.Success
                }
            }));

        }
    }
 ```

You can see I also append the `HX-Trigger` header to the response. This is a JSON object with the `showToast` key and a value of an object with the `toast` and `issuccess` keys. The `toast` key is the message to show in the toast notification and the `issuccess` key is a boolean indicating whether the action was successful or not.

Then in the `_Row` partial I have the HX (using HTMX.Net) attributes to trigger the action. 

```html
                     <!-- Revoke Login Tokens -->
                            <button class="btn btn-xs btn-error border whitespace-normal text-wrap tooltip tooltip-left" data-tip="Revoke login tokens"
                                    hx-get hx-indicator="#loading-modal" hx-target="closest tr" hx-swap="outerHTML"
                                    hx-action="Row" hx-controller="Users"
                                    hx-route-email="@user.Email" hx-route-useraction="@UserActionType.RevokeTokens"
                                    hx-confirm="Are you sure you want to revoke the login tokens for this user?">
                                <i class="bx bx-power-off"></i> Revoke
                            </button>
```

You can see I use the target `closest tr` to swap the entire row with the new content. This is a simple way to update the content of the row without having to do a full page refresh.

## Partial View
This is really very simple and a great technique for ASP.NET Core with HTMX. 
You can optionally use HTMX.Net`s `Request.IsHtmx` here but in this case I only ever use this from an HTMX callback. 

```csharp
    [Route("row")]
 
    public async Task<IActionResult> Row(string email, string? useraction = null)
    {

        if(!string.IsNullOrEmpty(useraction))
          await ApplyAction(email, useraction);

        var userRow = await userViewService.GetSingleUserViewModel(email);
        return PartialView("_Row", userRow);
    }
```

In this case the Partial view `_Row` is a simple table row with the user information and the buttons to perform the actions. 



# Additional HTMX features
I also use a couple of more HTMX features to make the user experience better.

## Loading
I also use a simple `loading modal` to indicate that the request is in progress. This is a simple way to show the user that something is happening in the background.

```html
<div id="loading-modal" class="modal htmx-indicator">
    <div
        class="modal-box flex flex-col items-center justify-center bg-base-200 border border-base-300 shadow-xl rounded-xl text-base-content dark text-center ">
        <div class="flex flex-col items-center space-y-4">
            <h2 class="text-lg font-semibold tracking-wide">Loading...</h2>
            <span class="loading loading-dots loading-xl text-4xl text-stone-200"></span>
        </div>
    </div>
</div>
```

## Confirm
I also use the `hx-confirm` attribute to show a confirmation dialog before the action is performed. This is a simple way to ensure that the user really wants to perform the action. This uses [SweetAlert2](https://sweetalert2.github.io/) to show a confirmation dialog. 

Now if you DON'T do this, HTMX still works but it uses the standard Browser 'confirm' dialog which can be a bit jarring for the user. 

 
```javascript
// HTMX confirm with SweetAlert2
window.addEventListener('htmx:confirm', (e) => {
    const message = e.detail.question;
    if (!message) return;

    e.preventDefault();

    Swal.fire({
        title: 'Please confirm',
        text: message,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Yes',
        cancelButtonText: 'Cancel',
        theme: 'dark',
    }).then(({ isConfirmed }) => {
        if (isConfirmed) e.detail.issueRequest(true);
    });
});
```

# In Conclusion
This is a simple way to use HTMX to show a toast notification and swap content on the page. This is a great way to make your web applications more dynamic and responsive.