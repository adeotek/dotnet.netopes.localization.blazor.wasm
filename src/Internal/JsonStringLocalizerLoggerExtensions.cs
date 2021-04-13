using System;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Netopes.Localization.Blazor.Wasm.Internal
{
    internal static class JsonStringLocalizerLoggerExtensions
    {
        private static readonly Action<ILogger, string, string, CultureInfo, Exception> _searchedLocation;
        private static readonly Action<ILogger, string, string, CultureInfo, Exception> _missingFromLocation;

        static JsonStringLocalizerLoggerExtensions()
        {
            _searchedLocation = LoggerMessage.Define<string, string, CultureInfo>(
                LogLevel.Debug,
                1,
                $"{nameof(JsonStringLocalizer)} searched for '{{Key}}' in '{{LocationSearched}}' with culture '{{Culture}}'.");
            _missingFromLocation = LoggerMessage.Define<string, string, CultureInfo>(
                LogLevel.Warning,
                1,
                $"{nameof(JsonStringLocalizer)} searched for '{{Key}}' in '{{LocationSearched}}' with culture '{{Culture}}', but is missing or is null.");
        }

        public static void SearchedLocation(this ILogger logger, string key, string searchedLocation, CultureInfo culture, string value)
        {
            if (value == null)
            {
                _missingFromLocation(logger, key, searchedLocation, culture, null);
            }
            else
            {
                _searchedLocation(logger, key, searchedLocation, culture, null);
            }
        }
    }
}
