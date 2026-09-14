using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

using Eiliko.Blazor.hCaptcha.Configurations;
using Eiliko.Blazor.hCaptcha.Enums;

namespace Eiliko.Blazor.hCaptcha
{
    public partial class HCaptcha : IAsyncDisposable
    {
        private const string ModulePath = "./_content/Eiliko.Blazor.hCaptcha/scripts/hCaptcha.js";

        [Inject] protected IJSRuntime JsRuntime { get; set; } = default!;

        [Inject] protected IHttpClientFactory HttpClientFactory { get; set; } = default!;

        [Inject] protected IOptionsMonitor<HCaptchaConfiguration> Configuration { get; set; } = default!;

        /// <summary>Invoked after every attempt with <c>true</c> only when the token was verified server-side.</summary>
        [Parameter] public EventCallback<bool> Callback { get; set; }

        /// <summary>Invoked after every attempt with the full verification result, including error codes.</summary>
        [Parameter] public EventCallback<HCaptchaVerificationResult> OnVerified { get; set; }

        [Parameter] public Theme Theme { get; set; }

        [Parameter] public Size Size { get; set; }

        /// <summary>Optional client IP address forwarded to hCaptcha as <c>remoteip</c>.</summary>
        [Parameter] public string? RemoteIp { get; set; }

        /// <summary>
        /// Attributes applied to the element the widget renders into, so it can be styled or
        /// sized directly, e.g. to reserve its footprint and avoid layout shift.
        /// <c>id</c> is ignored: the component owns it and hCaptcha renders into it.
        /// </summary>
        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

        protected string ID { get; } = "hcaptcha-" + Guid.NewGuid().ToString("N");

        private readonly CancellationTokenSource _disposal = new();
        private DotNetObjectReference<HCaptcha>? _instance;
        private IJSObjectReference? _module;
        private string? _widgetId;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender)
                return;

            try
            {
                var ct = _disposal.Token;
                var options = Configuration.CurrentValue;

                _module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", ct, ModulePath);
                _instance = DotNetObjectReference.Create(this);

                _widgetId = await _module.InvokeAsync<string?>("render", ct,
                    _instance,
                    ID,
                    options.SiteKey,
                    Theme.ToString().ToLowerInvariant(),
                    Size.ToString().ToLowerInvariant(),
                    options.ScriptLoadTimeout.TotalMilliseconds);

                if (_widgetId is null)
                    await NotifyAsync(HCaptchaVerificationResult.Failed("hcaptcha-script-not-loaded"));
            }
            catch (Exception ex) when (ex is JSDisconnectedException or OperationCanceledException)
            {
                // Circuit dropped or component disposed while initialising; nothing left to do.
            }
        }

        /// <summary>Resets the widget so the user can solve a new challenge, e.g. after a failed form submission.</summary>
        public async Task ResetAsync()
        {
            if (_module is null || _widgetId is null)
                return;

            try
            {
                await _module.InvokeVoidAsync("reset", _disposal.Token, _widgetId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or OperationCanceledException)
            {
            }
        }

        [JSInvokable("HCaptchaOnSuccess")]
        public async Task OnSuccess(string token)
        {
            var result = await VerifyAsync(token, _disposal.Token);
            await NotifyAsync(result);
        }

        [JSInvokable("HCaptchaOnError")]
        public Task OnError(string? errorCode) =>
            NotifyAsync(HCaptchaVerificationResult.Failed(string.IsNullOrWhiteSpace(errorCode) ? "unknown-error" : errorCode));

        [JSInvokable("HCaptchaOnExpired")]
        public Task OnExpired() =>
            NotifyAsync(HCaptchaVerificationResult.Failed("token-expired"));

        private async Task<HCaptchaVerificationResult> VerifyAsync(string token, CancellationToken ct)
        {
            var options = Configuration.CurrentValue;

            var fields = new Dictionary<string, string>
            {
                ["response"] = token,
                ["secret"] = options.Secret,
                ["sitekey"] = options.SiteKey,
            };

            if (!string.IsNullOrWhiteSpace(RemoteIp))
                fields["remoteip"] = RemoteIp;

            SiteVerifyResponse? payload;
            try
            {
                var client = HttpClientFactory.CreateClient(HCaptchaDefaults.HttpClientName);
                using var response = await client.PostAsync(options.VerifyUrl, new FormUrlEncodedContent(fields), ct);

                if (!response.IsSuccessStatusCode)
                    return HCaptchaVerificationResult.Failed($"siteverify-http-{(int)response.StatusCode}");

                payload = await response.Content.ReadFromJsonAsync<SiteVerifyResponse>(ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
            {
                return HCaptchaVerificationResult.Failed("siteverify-request-failed");
            }

            if (payload is null)
                return HCaptchaVerificationResult.Failed("siteverify-empty-response");

            var success = payload.Success;
            var errorCodes = new List<string>(payload.ErrorCodes ?? Array.Empty<string>());

            if (success
                && !string.IsNullOrWhiteSpace(options.ExpectedHostname)
                && !string.Equals(payload.Hostname, options.ExpectedHostname, StringComparison.OrdinalIgnoreCase))
            {
                success = false;
                errorCodes.Add("hostname-mismatch");
            }

            return new HCaptchaVerificationResult
            {
                Success = success,
                Hostname = payload.Hostname,
                ChallengeTimestamp = payload.ChallengeTimestamp,
                ErrorCodes = errorCodes,
            };
        }

        private async Task NotifyAsync(HCaptchaVerificationResult result)
        {
            if (_disposal.IsCancellationRequested)
                return;

            await OnVerified.InvokeAsync(result);
            await Callback.InvokeAsync(result.Success);
        }

        public async ValueTask DisposeAsync()
        {
            _disposal.Cancel();

            try
            {
                if (_module is not null)
                {
                    if (_widgetId is not null)
                        await _module.InvokeVoidAsync("remove", _widgetId);

                    await _module.DisposeAsync();
                }
            }
            catch (Exception ex) when (ex is JSDisconnectedException or OperationCanceledException)
            {
                // Circuit already gone; the browser has discarded the widget with it.
            }

            _instance?.Dispose();
            _disposal.Dispose();
        }

        private sealed class SiteVerifyResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("challenge_ts")]
            public DateTimeOffset? ChallengeTimestamp { get; set; }

            [JsonPropertyName("hostname")]
            public string? Hostname { get; set; }

            [JsonPropertyName("error-codes")]
            public string[]? ErrorCodes { get; set; }
        }
    }
}
