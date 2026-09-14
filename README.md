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

## How verification works

The token produced by the widget is never trusted on its own. On every solve the component POSTs the token, your secret and your site key to hCaptcha's `siteverify` endpoint from the server and only reports success when the response says `success: true` (and, if configured, the reported hostname matches `ExpectedHostname`). The secret never reaches the browser.

If hCaptcha's `api.js` does not load within `ScriptLoadTimeout` (default 10 seconds, e.g. because it is blocked by an ad blocker), the callbacks fire with a failed result carrying the error code `hcaptcha-script-not-loaded`.
