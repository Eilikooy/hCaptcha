## Eiliko.Blazor.hCaptcha

![NuGet](https://img.shields.io/nuget/v/Eiliko.Blazor.hCaptcha?logo=NuGet&label=NuGet%20%7C%20Eiliko.Blazor.hCaptcha&logoColor=blue&color=blue)

An hCaptcha component for server-side Blazor, with the token verified on the server. Originally forked from [Texnomic.Blazor.hCaptcha](https://github.com/Texnomic/hCaptcha).

> **1.0.0 is a security release.** Every earlier version reported a captcha as solved whenever hCaptcha's verification endpoint answered with any HTTP 2xx, without reading the response body that says whether the token was actually valid. The captcha could be bypassed completely. If you are on 0.4.x or older, upgrade and read [Upgrading from 0.4.x](#upgrading-from-04x).

## Installation

```pwsh
PM> Install-Package Eiliko.Blazor.hCaptcha
```

## Setup

**1. Reference hCaptcha's script** in `Components/App.razor`. The component's own JavaScript is an ES module that it imports itself, so this is the only tag you need:

```html
<head>
    <script src="https://js.hcaptcha.com/1/api.js?render=explicit" async defer></script>
</head>
```

**2. Register the service** in `Program.cs`:

```csharp
using Eiliko.Blazor.hCaptcha.Extensions;

builder.Services.AddHCaptcha(options =>
{
    options.SiteKey = builder.Configuration["hCaptcha:SiteKey"]!;
    options.Secret  = builder.Configuration["hCaptcha:Secret"]!;

    // Optional: reject a token that was solved on any other host.
    // options.ExpectedHostname = "www.example.com";
});
```

`AddHCaptcha` registers its own named `HttpClient` and validates `SiteKey` and `Secret` at startup, so a missing value fails immediately rather than at first render. Keep the secret in user secrets or environment configuration; it is used only on the server and never reaches the browser.

**3. Drop the component into a form.** Gate your submit on the result and reset the widget afterwards, because hCaptcha tokens are single use:

```razor
@using Eiliko.Blazor.hCaptcha
@using Eiliko.Blazor.hCaptcha.Enums
@inject ILogger<ContactForm> Logger

<EditForm Model="Model" OnValidSubmit="SubmitAsync">
    <InputText @bind-Value="Model.Email" />

    <HCaptcha @ref="_captcha"
              Theme="Theme.Dark"
              OnVerified="OnVerified"
              style="width: 303px; min-height: 78px;" />

    <button type="submit" disabled="@(!_solved)">Send</button>
</EditForm>

@code {
    private HCaptcha? _captcha;
    private bool _solved;

    private void OnVerified(HCaptchaVerificationResult result)
    {
        _solved = result.Success;

        if (!result.Success)
            Logger.LogWarning("hCaptcha rejected the token: {Codes}",
                string.Join(", ", result.ErrorCodes));
    }

    private async Task SubmitAsync()
    {
        if (!_solved)
            return;

        // ... handle the submission ...

        _solved = false;
        await _captcha!.ResetAsync();
    }
}
```

`OnVerified` gives you the reason a captcha failed. If you only need the yes or no, bind `Callback` instead and take a `bool`.

The `style` above reserves the widget's footprint so the form does not jump when the widget appears. Any attribute you put on the component reaches the element hCaptcha renders into.

## Upgrading from 0.4.x

Verification is the reason to upgrade, and it is not an opt-in change: tokens are now genuinely checked. Five things affect existing setups.

**Some submissions that used to succeed will now fail.** That is the fix working. Forged, expired and already-used tokens were all accepted before. Bind `OnVerified` and log `ErrorCodes` if you want to see why something was rejected.

**`Size` now takes effect.** The size option previously reached hCaptcha under a misspelled key and was discarded, so every widget rendered `Normal` regardless of the markup. If yours sets `Size="Size.Compact"` it will now genuinely render compact, which looks roughly square instead of a wide bar. Remove the parameter or set `Size="Size.Normal"` to keep the old appearance.

**Remove the second script tag.** Delete the `_content/Eiliko.Blazor.hCaptcha/scripts/hCaptcha.js` tag from `App.razor`. That file is now an ES module the component imports itself, and loading it as a classic script throws a syntax error. Keep the hCaptcha `api.js` tag.

**`AddHttpClient()` is no longer needed** for this component. Calling it anyway is harmless.

**The component no longer needs to be gated on script readiness.** It waits for `api.js` on its own for up to `ScriptLoadTimeout`, so you can render it immediately and drop any "is hCaptcha loaded yet" flag. It does not fetch `api.js`, so the script tag, or your own loader, still has to do that.

Nothing else in the public API was removed. `Callback`, `Theme` and `Size` all behave as before.

## Component parameters

| Parameter | Type | Default | Purpose |
|---|---|---|---|
| `Callback` | `EventCallback<bool>` | none | Fires after every attempt. `true` only when the token was verified server-side. |
| `OnVerified` | `EventCallback<HCaptchaVerificationResult>` | none | Same moment as `Callback`, with the full result instead of a bool. |
| `Theme` | `Theme` | `Light` | `Light` or `Dark`. |
| `Size` | `Size` | `Normal` | `Normal` renders 303x78, `Compact` renders 164x144. |
| `RemoteIp` | `string` | `null` | Client IP address forwarded to hCaptcha as `remoteip`, which improves its scoring. Optional. |
| any other attribute | | | Applied to the element the widget renders into, so `class` and `style` reach it directly. `id` is ignored because the component owns it. |

`ResetAsync()` clears a used token so the visitor can solve a new challenge. Call it after any failed submission.

## Configuration options

| Option | Type | Default | Purpose |
|---|---|---|---|
| `SiteKey` | `string` | required | Public site key, sent to the browser and included in every verification request. |
| `Secret` | `string` | required | Account secret. Used server-side only and never sent to the browser. |
| `ExpectedHostname` | `string` | `null` | When set, a token is rejected unless hCaptcha reports it was solved on this host. Case-insensitive. |
| `VerifyUrl` | `Uri` | `https://api.hcaptcha.com/siteverify` | Verification endpoint. Override only for testing. |
| `ScriptLoadTimeout` | `TimeSpan` | 10 seconds | How long to wait for hCaptcha's `api.js` before reporting failure. |

## Verification result

`OnVerified` receives an `HCaptchaVerificationResult` with `Success`, `Hostname`, `ChallengeTimestamp` and `ErrorCodes`. `Success` is true only when hCaptcha confirmed the token, and the hostname matched if you configured one.

`ErrorCodes` carries hCaptcha's own codes plus these, raised by the component itself:

| Code | Meaning |
|---|---|
| `hcaptcha-script-not-loaded` | `api.js` did not load within `ScriptLoadTimeout`, often an ad blocker or a content security policy. |
| `token-expired` | The visitor solved the challenge but waited too long to submit. |
| `hostname-mismatch` | The token was solved on a host other than `ExpectedHostname`. |
| `siteverify-request-failed` | The call to hCaptcha could not be completed or returned unreadable content. |
| `siteverify-http-<status>` | hCaptcha answered with a non-success HTTP status. |
| `siteverify-empty-response` | hCaptcha answered with an empty body. |

## How verification works

The token from the widget is never trusted on its own. On every solve, the component posts the token, your secret and your site key to hCaptcha's `siteverify` endpoint from the server, parses the response, and reports success only when the body says `success: true`. Sending the site key means a token issued for a different site cannot be replayed against yours, and `ExpectedHostname` additionally pins where the challenge was solved.

An HTTP 2xx on its own means nothing here. hCaptcha answers `200 OK` with `"success": false` for invalid, expired and already-redeemed tokens, which is precisely what earlier versions mistook for a pass.

## Loading hCaptcha only where it is needed

`api.js` is a third-party script that sees the visitor's address and browser details, so you may not want it on every page. Because the component waits for the global to appear rather than requiring it up front, you can inject the script yourself from the page that hosts the form and render the component straight away. If the script never arrives, the callbacks report `hcaptcha-script-not-loaded` instead of the widget silently never appearing.
