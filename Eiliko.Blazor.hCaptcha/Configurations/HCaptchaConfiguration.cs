using System;

namespace Eiliko.Blazor.hCaptcha.Configurations
{
    public class HCaptchaConfiguration
    {
        /// <summary>The hCaptcha site key rendered into the widget and sent with every verification request.</summary>
        public string SiteKey { get; set; } = string.Empty;

        /// <summary>The hCaptcha secret. Only ever used server-side.</summary>
        public string Secret { get; set; } = string.Empty;

        /// <summary>
        /// When set, a token is only accepted if the hostname reported by hCaptcha matches this value (case-insensitive).
        /// Leave null to accept any hostname.
        /// </summary>
        public string? ExpectedHostname { get; set; }

        /// <summary>The siteverify endpoint. Override only for testing.</summary>
        public Uri VerifyUrl { get; set; } = new("https://api.hcaptcha.com/siteverify");

        /// <summary>How long the component waits for hCaptcha's api.js to load before reporting failure.</summary>
        public TimeSpan ScriptLoadTimeout { get; set; } = TimeSpan.FromSeconds(10);
    }
}
