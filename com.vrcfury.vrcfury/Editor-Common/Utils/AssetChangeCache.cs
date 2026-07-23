using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VF.Utils {
    internal class AssetChangeCache<TResult> {
        private class Entry {
            public string assetPath;
            public Hash128 dependencyHash;
            public Dictionary<Object, int> dirtyCounts;
            public TResult result;

            public bool IsValid(Object asset) {
                var currentPath = AssetDatabase.GetAssetPath(asset);
                if (currentPath != assetPath) return false;
                if (!string.IsNullOrEmpty(assetPath)
                    && AssetDatabase.GetAssetDependencyHash(assetPath) != dependencyHash) return false;
                return dirtyCounts.All(pair =>
                    pair.Key != null && EditorUtility.GetDirtyCount(pair.Key) == pair.Value
                );
            }
        }

        private readonly ConditionalWeakTable<Object, Entry> entries =
            new ConditionalWeakTable<Object, Entry>();
        private readonly Type[] dependencyTypes;

        public AssetChangeCache(params Type[] dependencyTypes) {
            this.dependencyTypes = dependencyTypes ?? Array.Empty<Type>();
        }

        public TResult Get(Object asset, Func<TResult> factory) {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (entries.TryGetValue(asset, out var entry) && entry.IsValid(asset)) {
                return entry.result;
            }

            entries.Remove(asset);
            var assetPath = AssetDatabase.GetAssetPath(asset);
            var observedObjects = new HashSet<Object> { asset };
            if (dependencyTypes.Length > 0) {
                observedObjects.UnionWith(MutableManager.GetDependencies(asset, dependencyTypes));
            }

            var result = factory();
            entry = new Entry {
                assetPath = assetPath,
                dependencyHash = string.IsNullOrEmpty(assetPath)
                    ? default
                    : AssetDatabase.GetAssetDependencyHash(assetPath),
                dirtyCounts = observedObjects.ToDictionary(obj => obj, EditorUtility.GetDirtyCount),
                result = result
            };
            entries.Add(asset, entry);
            return result;
        }
    }
}
