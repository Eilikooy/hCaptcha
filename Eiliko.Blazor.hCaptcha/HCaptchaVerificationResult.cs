using System;
using System.Collections.Generic;

namespace Eiliko.Blazor.hCaptcha
{
    /// <summary>Outcome of a captcha attempt, as reported by hCaptcha's siteverify endpoint or by the widget itself.</summary>
    public sealed class HCaptchaVerificationResult
    {
        /// <summary>True only when siteverify confirmed the token (and the hostname matched, if <c>ExpectedHostname</c> is configured).</summary>
        public bool Success { get; init; }

        /// <summary>Hostname of the site where the challenge was solved, as reported by hCaptcha.</summary>
        public string? Hostname { get; init; }

        /// <summary>Timestamp of the challenge, as reported by hCaptcha.</summary>
        public DateTimeOffset? ChallengeTimestamp { get; init; }

        /// <summary>
        /// hCaptcha error codes, plus the component's own codes:
        /// <c>hcaptcha-script-not-loaded</c>, <c>token-expired</c>, <c>hostname-mismatch</c>,
        /// <c>siteverify-request-failed</c>, <c>siteverify-http-{status}</c>, <c>siteverify-empty-response</c>.
        /// </summary>
        public IReadOnlyList<string> ErrorCodes { get; init; } = Array.Empty<string>();

        public static HCaptchaVerificationResult Failed(params string[] errorCodes) =>
            new() { Success = false, ErrorCodes = errorCodes };
    }
}
