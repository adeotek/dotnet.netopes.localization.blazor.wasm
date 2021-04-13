using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Netopes.Localization.Blazor.Wasm
{
    public class JsonStringLocalizerFactory : IStringLocalizerFactory
    {
        private readonly string _resourcesRelativePath;
        private readonly ResourcesType _resourcesType;
        private readonly ResourcesLoadMode _resourcesLoadMode;
        private readonly ResourcesCacheMode _resourcesCacheMode;
        private readonly JsonStringLocalizerInMemoryCache _jsonStringLocalizerInMemoryCache;
        private readonly ILoggerFactory _loggerFactory;

        public JsonStringLocalizerFactory(
            IOptions<JsonLocalizationOptions> localizationOptions,
            JsonStringLocalizerInMemoryCache jsonStringLocalizerInMemoryCache,
            ILoggerFactory loggerFactory)
        {
            if (localizationOptions == null)
            {
                throw new ArgumentNullException(nameof(localizationOptions));
            }
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _resourcesRelativePath = localizationOptions.Value.ResourcesPath;
            _resourcesType = localizationOptions.Value.ResourcesType;
            _resourcesLoadMode = localizationOptions.Value.ResourcesLoadMode;
            _resourcesCacheMode = localizationOptions.Value.ResourcesCacheMode;
            _jsonStringLocalizerInMemoryCache = jsonStringLocalizerInMemoryCache;
        }

        public IStringLocalizer Create(Type resourceSource)
        {
            if (resourceSource == null)
            {
                throw new ArgumentNullException(nameof(resourceSource));
            }

            var typeInfo = resourceSource.GetTypeInfo();
            var assembly = typeInfo.Assembly;
            var assemblyName = resourceSource.Assembly.GetName().Name;
            var typeName = $"{assemblyName}.{typeInfo.Name}" == typeInfo.FullName || $"{assemblyName}.{typeInfo.Name}".Length > (typeInfo.FullName ?? string.Empty).Length
                ? typeInfo.Name
                : typeInfo.FullName.Substring(assemblyName.Length + 1);

            var resourcesPath = _resourcesLoadMode == ResourcesLoadMode.File 
                ? Path.Combine(Internal.Helpers.GetApplicationRoot(), GetResourcePath(assembly)) 
                : GetResourcePath();
            typeName = TryFixInnerClassPath(typeName);

            return CreateJsonStringLocalizer(resourcesPath, typeName, assembly);
        }

        public IStringLocalizer Create(string baseName, string location)
        {
            if (baseName == null)
            {
                throw new ArgumentNullException(nameof(baseName));
            }

            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            baseName = TryFixInnerClassPath(baseName);

            var assemblyName = new AssemblyName(location);
            var assembly = Assembly.Load(assemblyName);
            
            var resourcesPath = _resourcesLoadMode == ResourcesLoadMode.File
                ? Path.Combine(Internal.Helpers.GetApplicationRoot(), GetResourcePath(assembly))
                : GetResourcePath();
            string resourceName = null;

            if (_resourcesType != ResourcesType.CultureBased)
            {
                resourceName = TrimPrefix(baseName, $"{location}.");
            }

            return CreateJsonStringLocalizer(resourcesPath, resourceName, assembly);
        }

        protected virtual JsonStringLocalizer CreateJsonStringLocalizer(
            string resourcesPath,
            string resourceName,
            Assembly assembly)
        {
            var logger = _loggerFactory.CreateLogger<JsonStringLocalizer>();

            return new JsonStringLocalizer(
                resourcesPath, _resourcesType != ResourcesType.CultureBased ? resourceName : null,
                _resourcesType,
                _resourcesLoadMode,
                _resourcesCacheMode,
                _jsonStringLocalizerInMemoryCache,
                assembly,
                logger);
        }

        private string GetResourcePath(Assembly assembly)
        {
            var resourceLocationAttribute = assembly.GetCustomAttribute<ResourceLocationAttribute>();

            return resourceLocationAttribute == null
                ? _resourcesRelativePath
                : resourceLocationAttribute.ResourceLocation;
        }
        
        private string GetResourcePath() => string.IsNullOrWhiteSpace(_resourcesRelativePath) ? string.Empty : _resourcesRelativePath.Trim('/', '\\', ' ');

        private static string TrimPrefix(string name, string prefix) =>  name.StartsWith(prefix, StringComparison.Ordinal) ? name.Substring(prefix.Length) : name;

        private string TryFixInnerClassPath(string path)
        {
            const char innerClassSeparator = '+';
            var fixedPath = path;

            if (path.Contains(innerClassSeparator.ToString()))
            {
                fixedPath = path.Replace(innerClassSeparator, '.');
            }

            return fixedPath;
        }
    }
}