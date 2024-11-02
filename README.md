## Eiliko.Blazor.hCaptcha

![NuGet](https://img.shields.io/nuget/vpre/Eiliko.Blazor.hCaptcha?logo=NuGet&label=NuGet%20%7C%20Eiliko.Blazor.hCaptcha&logoColor=blue&color=blue)

ASP.NET Core hCaptcha Component for Server-Side Blazor. Updated version of [Texnomic.Blazor.hCaptcha](https://github.com/Texnomic/hCaptcha)

## Installation

```pwsh
PM> Install-Package Eiliko.Blazor.hCaptcha
```

## Setup


1. Reference hCaptcha & NuGet Package JavaScript Files In `Components/App.razor` File:

    ```html
    <head>

    <script src="https://hcaptcha.com/1/api.js&render=explicit" async type="text/javascript"></script>

    <script src="_content/Eiliko.Blazor.hCaptcha/scripts/hCaptcha.js" type="text/javascript"></script>

    </head>
    ```

2. Add Package Configuration To Dependancy Injection Services in `Program.cs` File:

    ```csharp
    using Eiliko.Blazor.hCaptcha.Extensions;


        builder.Services.AddHttpClient();
        builder.Services.AddHCaptcha(Options =>
        {
            Options.SiteKey = "10000000-ffff-ffff-ffff-000000000001";
            Options.Secret = "0x0000000000000000000000000000000000000000";
        });
    ```

3. Create Callback Function & Backing Field To Capture Captcha Result In `Example.razor` File:

    ```csharp
    private bool IsCaptchaValid { get; set; }

    protected void hCaptchaCallback(bool Result) => IsCaptchaValid = Result;
    ```

4. Finally, Drop-In hCaptcha Component & Bind Callback Function In `Example.razor` File:

    ```html
    <HCaptcha Callback="hCaptchaCallback" Theme="Theme.Dark"></HCaptcha>
    ```