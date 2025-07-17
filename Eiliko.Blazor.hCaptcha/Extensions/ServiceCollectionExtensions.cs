using System;
using Microsoft.Extensions.DependencyInjection;
using Eiliko.Blazor.hCaptcha.Configurations;

namespace Eiliko.Blazor.hCaptcha.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddHCaptcha(this IServiceCollection Services, Action<HCaptchaConfiguration> Configuration)
        {
            Services.Configure<HCaptchaConfiguration>(Configuration);
            return Services;
        }

    }
}
