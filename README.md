## Eiliko.Blazor.hCaptcha

![NuGet](https://img.shields.io/nuget/vpre/Eiliko.Blazor.hCaptcha?logo=NuGet&label=NuGet%20%7C%20Eiliko.Blazor.hCaptcha&logoColor=blue&color=blue)

ASP.NET Core hCaptcha Component for Server-Side Blazor. Updated version of [Texnomic.Blazor.hCaptcha](https://github.com/Texnomic/hCaptcha)

## Installation

```pwsh
PM> Install-Package Eiliko.Blazor.hCaptcha
```

## Setup


1. Reference the hCaptcha JavaScript API in `Components/App.razor`. The component's own script is an ES module that is loaded automatically, so no second script tag is needed:

    ```html
    <head>

    <script src="https://js.hcaptcha.com/1/api.js?render=explicit" async defer type="text/javascript"></script>

    </head>
    ```

2. Add Package Configuration To Dependency Injection Services in `Program.cs` File:

    ```csharp
    using Eiliko.Blazor.hCaptcha.Extensions;


        builder.Services.AddHCaptcha(options =>
        {
            options.SiteKey = "10000000-ffff-ffff-ffff-000000000001";
            options.Secret = "0x0000000000000000000000000000000000000000";

            // Optional: reject tokens solved on any other host.
            // options.ExpectedHostname = "www.example.com";
        });
    ```

    `AddHCaptcha` registers its own named `HttpClient` and validates that `SiteKey` and `Secret` are set at startup.

3. Create Callback Function & Backing Field To Capture Captcha Result In `Example.razor` File:

    ```csharp
    private bool IsCaptchaValid { get; set; }

    protected void hCaptchaCallback(bool Result) => IsCaptchaValid = Result;
    ```

    If you want the details (hostname, timestamp, error codes) use `OnVerified` instead of, or in addition to, `Callback`:

    ```csharp
    protected void hCaptchaVerified(HCaptchaVerificationResult result)
    {
        IsCaptchaValid = result.Success;
        if (!result.Success)
            Logger.LogWarning("hCaptcha failed: {Errors}", string.Join(", ", result.ErrorCodes));
    }
    ```

4. Finally, Drop-In hCaptcha Component & Bind Callback Function In `Example.razor` File:

    ```html
    <HCaptcha @ref="captcha" Callback="hCaptchaCallback" Theme="Theme.Dark"></HCaptcha>
    ```

    Tokens are single-use. After a failed form submission call `await captcha.ResetAsync()` so the user can solve a new challenge.

## Upgrading from 0.4.x

1.0.0 fixes a vulnerability: the result of hCaptcha's verification call was never checked, so any token, including one never solved, was reported as valid. Upgrading is strongly recommended. Four changes affect existing setups.

**`Size` now takes effect.** In 0.4.x the size option was handed to hCaptcha under a misspelled key and silently ignored, so every widget rendered at `Normal` no matter what the markup said. If your component sets `Size="Size.Compact"`, the widget will now genuinely render compact, which looks roughly square rather than a wide bar. Remove the parameter or set `Size="Size.Normal"` to keep the previous appearance.

**Remove the second script tag.** The component loads its own JavaScript as a module. Delete the `_content/Eiliko.Blazor.hCaptcha/scripts/hCaptcha.js` tag from `App.razor`. Keep the hCaptcha `api.js` tag.

**`AddHttpClient()` is no longer required** for this component. `AddHCaptcha` registers its own named client. Calling it anyway is harmless.

**Some submissions that used to pass will now fail.** That is the point of the fix. Expired, replayed and forged tokens were previously accepted and are now rejected. Use `OnVerified` to log `ErrorCodes` if you want to see why.

## Component parameters

| Parameter | Type | Default | Purpose |
|---|---|---|---|
| `Callback` | `EventCallback<bool>` | none | Fires after every attempt. `true` only when the token was verified server-side. |
| `OnVerified` | `EventCallback<HCaptchaVerificationResult>` | none | Same moment as `Callback`, with the full result instead of a bool. |
| `Theme` | `Theme` | `Light` | `Light` or `Dark`. |
| `Size` | `Size` | `Normal` | `Normal` is a wide bar, `Compact` is a small square block. |
| `RemoteIp` | `string` | `null` | Client IP address forwarded to hCaptcha as `remoteip`, which improves its own scoring. Optional. |
| any other attribute | | | Applied to the element the widget renders into, so `class` and `style` reach it directly. `id` is ignored because the component owns it. |

Because attributes are passed through, the widget's footprint can be reserved on the component itself, which avoids the form shifting when the widget appears:

```html
<HCaptcha Size="Size.Compact" Theme="Theme.Dark" Callback="hCaptchaCallback"
          style="width: 164px; min-height: 144px;" />
```

Compact renders at 164x144 and normal at 303x78.

`ResetAsync()` clears a used token so the visitor can solve a new challenge. hCaptcha tokens are single-use, so call it after any failed submission.

## Configuration options

Set on `AddHCaptcha`. `SiteKey` and `Secret` are validated when the application starts, so a missing value fails fast instead of at first render.

| Option | Type | Default | Purpose |
|---|---|---|---|
| `SiteKey` | `string` | required | Public site key, sent to the browser and included in every verification request. |
| `Secret` | `string` | required | Account secret. Used server-side only and never sent to the browser. |
| `ExpectedHostname` | `string` | `null` | When set, a token is rejected unless hCaptcha reports it was solved on this host. Case-insensitive. |
| `VerifyUrl` | `Uri` | `https://api.hcaptcha.com/siteverify` | Verification endpoint. Override only for testing. |
| `ScriptLoadTimeout` | `TimeSpan` | 10 seconds | How long to wait for hCaptcha's `api.js` before reporting failure. |

## Verification result

`OnVerified` receives an `HCaptchaVerificationResult` with `Success`, `Hostname`, `ChallengeTimestamp` and `ErrorCodes`. `Success` is true only when hCaptcha confirmed the token and the hostname matched, if you configured one.

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

The token produced by the widget is never trusted on its own. On every solve the component POSTs the token, your secret and your site key to hCaptcha's `siteverify` endpoint from the server and only reports success when the response says `success: true` (and, if configured, the reported hostname matches `ExpectedHostname`). The secret never reaches the browser.

If hCaptcha's `api.js` does not load within `ScriptLoadTimeout` (default 10 seconds, e.g. because it is blocked by an ad blocker), the callbacks fire with a failed result carrying the error code `hcaptcha-script-not-loaded`.
