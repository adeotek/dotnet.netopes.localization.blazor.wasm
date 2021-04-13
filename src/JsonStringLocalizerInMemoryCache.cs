using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Netopes.Localization.Blazor.Wasm
{
    public class JsonStringLocalizerInMemoryCache : IDisposable
    {
        private readonly ConcurrentDictionary<string, IEnumerable<KeyValuePair<string, string>>> _data;

        public JsonStringLocalizerInMemoryCache()
        {
            _data = new ConcurrentDictionary<string, IEnumerable<KeyValuePair<string, string>>>();
        }

        public IEnumerable<KeyValuePair<string, string>> Get(string key) => _data?[key] ?? new List<KeyValuePair<string, string>>();

        public bool TryGet(string key, out IEnumerable<KeyValuePair<string, string>> data)
        {
            if (!_data.ContainsKey(key) || _data[key] == null)
            {
                data = null;
                return false;
            }

            data = _data[key];
            return true;
        }

        public IEnumerable<KeyValuePair<string, string>> AddAndGet(string key, IEnumerable<KeyValuePair<string, string>> data) => _data[key] = data;

        public void Dispose()
        {
            _data.Clear();
        }
    }
}