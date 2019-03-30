using System;
using Microsoft.Extensions.Configuration;
using Rebus.Config;
using Rebus.Configuration.Settings;

namespace Rebus.Configuration
{
    public static class RebusCommonConfigurationExtension
    {
        public static RebusConfigurer ConfigureFrom(this RebusConfigurer source, IConfiguration configuration, string rootConfigurationSectionName = "Rebus")
        {
            var rootSection = configuration.GetSection(rootConfigurationSectionName) ??
                throw new ArgumentException($"Provided root section name {rootConfigurationSectionName} is invalid",
                    nameof(rootConfigurationSectionName));

            new RebusSettingsConfigurer(source, rootSection).ReadConfiguration();

            return source;
        }
    }
}
