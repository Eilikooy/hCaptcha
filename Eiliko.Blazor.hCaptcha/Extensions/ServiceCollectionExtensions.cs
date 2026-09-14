using System;
using Microsoft.Extensions.DependencyInjection;
using Eiliko.Blazor.hCaptcha.Configurations;

namespace Eiliko.Blazor.hCaptcha.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHCaptcha(this IServiceCollection services, Action<HCaptchaConfiguration> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.AddOptions<HCaptchaConfiguration>()
                .Configure(configure)
                .Validate(o => !string.IsNullOrWhiteSpace(o.SiteKey), "hCaptcha: SiteKey must be configured.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.Secret), "hCaptcha: Secret must be configured.")
                .Validate(o => o.VerifyUrl is { IsAbsoluteUri: true }, "hCaptcha: VerifyUrl must be an absolute URI.")
                .Validate(o => o.ScriptLoadTimeout > TimeSpan.Zero, "hCaptcha: ScriptLoadTimeout must be positive.")
                .ValidateOnStart();

            services.AddHttpClient(HCaptchaDefaults.HttpClientName, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(10);
            });

            return services;
        }
    }
}
