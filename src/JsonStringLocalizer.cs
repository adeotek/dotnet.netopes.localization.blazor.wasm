using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Netopes.Localization.Blazor.Wasm.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Netopes.Localization.Blazor.Wasm
{
    public class JsonStringLocalizer : IStringLocalizer
    {
        private readonly ConcurrentDictionary<string, IEnumerable<KeyValuePair<string, string>>> _resourcesCache = new ConcurrentDictionary<string, IEnumerable<KeyValuePair<string, string>>>();
        private readonly string _resourcesPath;
        private readonly string _resourceName;
        private readonly ResourcesType _resourcesType;
        private readonly ResourcesLoadMode _resourcesLoadMode;
        private readonly ResourcesCacheMode _resourcesCacheMode;
        private readonly JsonStringLocalizerInMemoryCache _jsonStringLocalizerInMemoryCache;
        private readonly Assembly _executingAssembly;
        private readonly ILogger _logger;

        private string _searchedLocation;

        public JsonStringLocalizer(
            string resourcesPath,
            string resourceName,
            ResourcesType resourcesType,
            ResourcesLoadMode resourcesLoadMode,
            ResourcesCacheMode resourcesCacheMode,
            JsonStringLocalizerInMemoryCache jsonStringLocalizerInMemoryCache,
            Assembly executingAssembly,
            ILogger logger)
        {
            _resourcesPath = resourcesPath ?? throw new ArgumentNullException(nameof(resourcesPath));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _resourceName = resourceName;
            _resourcesType = resourcesType;
            _resourcesLoadMode = resourcesLoadMode;
            _resourcesCacheMode = resourcesCacheMode;
            _jsonStringLocalizerInMemoryCache = jsonStringLocalizerInMemoryCache;
            _executingAssembly = executingAssembly;
        }

        public LocalizedString this[string name]
        {
            get
            {
                if (name == null)
                {
                    throw new ArgumentNullException(nameof(name));
                }

                var value = GetStringSafely(name);

                return new LocalizedString(name, value ?? name, resourceNotFound: value == null, searchedLocation: _searchedLocation);
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                if (name == null)
                {
                    throw new ArgumentNullException(nameof(name));
                }

                var format = GetStringSafely(name);
                var value = string.Format(format ?? name, arguments);

                return new LocalizedString(name, value, resourceNotFound: format == null, searchedLocation: _searchedLocation);
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            GetAllStrings(includeParentCultures, CultureInfo.CurrentUICulture);

        public IStringLocalizer WithCulture(CultureInfo culture) => this;

        protected IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures, CultureInfo culture)
        {
            if (culture == null)
            {
                throw new ArgumentNullException(nameof(culture));
            }

            var resourceNames = includeParentCultures
                ? GetAllStringsFromCultureHierarchy(culture)
                : GetAllResourceStrings(culture);

            foreach (var name in resourceNames)
            {
                var value = GetStringSafely(name);
                yield return new LocalizedString(name, value ?? name, resourceNotFound: value == null, searchedLocation: _searchedLocation);
            }
        }

        protected virtual string GetStringSafely(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var culture = CultureInfo.CurrentUICulture;
            string value = null;

            while (!Equals(culture, culture.Parent))
            {
                BuildResourcesCache(culture.Name);

                if (!_resourcesCache.TryGetValue(culture.Name, out var resources))
                {
                    continue;
                }

                var resource = resources?.SingleOrDefault(s => s.Key == name);

                value = resource?.Value;
                _logger.SearchedLocation(name, _searchedLocation, culture, value);

                if (value != null)
                {
                    break;
                }

                culture = culture.Parent;
            }

            return value;
        }

        private IEnumerable<string> GetAllStringsFromCultureHierarchy(CultureInfo startingCulture)
        {
            var currentCulture = startingCulture;
            var resourceNames = new HashSet<string>();

            while (!Equals(currentCulture, currentCulture.Parent))
            {
                var cultureResourceNames = GetAllResourceStrings(currentCulture);

                if (cultureResourceNames != null)
                {
                    foreach (var resourceName in cultureResourceNames)
                    {
                        resourceNames.Add(resourceName);
                    }
                }

                currentCulture = currentCulture.Parent;
            }

            return resourceNames;
        }

        private IEnumerable<string> GetAllResourceStrings(CultureInfo culture)
        {
            BuildResourcesCache(culture.Name);

            if (_resourcesCache.TryGetValue(culture.Name, out var resources))
            {
                foreach (var resource in resources)
                {
                    yield return resource.Key;
                }
            }
            else
            {
                yield return null;
            }
        }

        private void BuildResourcesCache(string culture)
        {
            if (_resourcesType != ResourcesType.Mixed)
            {
                _resourcesCache.GetOrAdd(culture, LoadResources(culture, string.IsNullOrEmpty(_resourceName)
                    ? $"{culture}.json"
                    : $"{_resourceName}.{culture}.json"));
                return;
            }
            
            _resourcesCache.GetOrAdd(culture, _ =>
            {
                var result = LoadResources(culture, $"{culture}.json");
                if (string.IsNullOrEmpty(_resourceName))
                {
                    return result;
                }
                var resultOverwrite = LoadResources(culture, $"{_resourceName}.{culture}.json")?.ToList();
                return (resultOverwrite?.Count ?? 0) == 0 ? result : result.ToList().Merge(resultOverwrite);
            });
        }
        
        private IEnumerable<KeyValuePair<string, string>> LoadResources(string culture, string resourceFile)
        {
            if (_resourcesCacheMode != ResourcesCacheMode.InMemory)
            {
                return _resourcesLoadMode == ResourcesLoadMode.EmbeddedResource
                    ? LoadResourcesFromEmbeddedResources(culture, resourceFile)
                    : LoadResourcesFromFile(culture, $"{culture}.json");
            }
            
            var cacheKey = _resourcesLoadMode == ResourcesLoadMode.EmbeddedResource ? resourceFile : $"{culture}.json";
            if (_jsonStringLocalizerInMemoryCache.TryGet(cacheKey, out var data))
            {
                return data;
            }

            return _jsonStringLocalizerInMemoryCache.AddAndGet(cacheKey,
                _resourcesLoadMode == ResourcesLoadMode.EmbeddedResource
                    ? LoadResourcesFromEmbeddedResources(culture, resourceFile)
                    : LoadResourcesFromFile(culture, $"{culture}.json"));
        }

        private IEnumerable<KeyValuePair<string, string>> LoadResourcesFromFile(string culture, string resourceFile)
        {
            _searchedLocation = Path.Combine(_resourcesPath, resourceFile);

            if (!File.Exists(_searchedLocation))
            {
                if (resourceFile.Count(r => r == '.') > 1)
                {
                    var resourceFileWithoutExtension = Path.GetFileNameWithoutExtension(resourceFile);
                    var resourceFileWithoutCulture = resourceFileWithoutExtension.Substring(0, resourceFileWithoutExtension.LastIndexOf('.'));
                    resourceFile = $"{resourceFileWithoutCulture.Replace('.', Path.DirectorySeparatorChar)}.{culture}.json";
                    _searchedLocation = Path.Combine(_resourcesPath, resourceFile);
                }
            }

            if (!File.Exists(_searchedLocation))
            {
                return null;
            }

            var builder = new ConfigurationBuilder()
                .SetBasePath(_resourcesPath)
                .AddJsonFile(resourceFile, optional: false, reloadOnChange: true);

            var config = builder.Build();
            return config.AsEnumerable();
        }
        
        private IEnumerable<KeyValuePair<string, string>> LoadResourcesFromEmbeddedResources(string culture, string resourceFile)
        {
            try
            {
                using var stream = _executingAssembly.GetManifestResourceStream($"{_executingAssembly.GetName().Name}.{_resourcesPath}.{resourceFile}");
                if (stream == null)
                {
                    return null;
                }

                using var reader = new StreamReader(stream);
                var resources = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(resources))
                {
                    return null;
                }
                        
                var content = JsonSerializer.Deserialize<Dictionary<string, string>>(resources);
                return content?
                    .Where(t => string.IsNullOrWhiteSpace(t.Key) == false)
                    .Select(t => new KeyValuePair<string, string>(t.Key, t.Value)).AsEnumerable();
            }
            catch (Exception e)
            {
                _logger?.LogError(e, $"Unable to load JSON localization resource for culture: {culture}");
                return null;
            }
        }
    }
}
