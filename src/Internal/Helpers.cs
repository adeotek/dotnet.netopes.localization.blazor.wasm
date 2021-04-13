using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Netopes.Localization.Blazor.Wasm.Internal
{
    public static class Helpers
    {
        public static string GetApplicationRoot()
            => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
    }

    static class Extensions
    {
        public static IList<T> Clone<T>(this IList<T> listToClone) where T : ICloneable
        {
            return listToClone.Select(item => (T)item.Clone()).ToList();
        }

        public static IList<KeyValuePair<string, string>> Merge(this IList<KeyValuePair<string, string>> mainList, IEnumerable<KeyValuePair<string, string>> secondaryList)
        {
            foreach (var newItem in secondaryList)
            {
                if (newItem.Key == null || newItem.Value == null)
                {
                    continue;
                }

                var index = mainList.ToList().FindIndex(item => item.Key == newItem.Key);
                if (index >= 0)
                {
                    mainList[index] = newItem;
                }
                else
                {
                    mainList.Add(newItem);
                }
            }

            return mainList;
        }
    }
}