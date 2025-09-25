using System;
using System.Collections.Generic;
using VF.Injector;

namespace VF.Service {
    /**
     * Keeps track of where added parameters came from, mostly used for quest sync alignment
     */
    [VFService]
    internal class ParameterSourceService {
        private readonly HashSet<Source> usedSources = new HashSet<Source>();
        private readonly Dictionary<string, Source> paramSources = new Dictionary<string, Source>();

        public void RecordParamSource(string paramName, string objectPath, string originalParamName) {
            var source = new Source { objectPath = objectPath, offset = 0, originalParamName = originalParamName };
            while (usedSources.Contains(source)) {
                source.offset++;
            }

            usedSources.Add(source);
            paramSources[paramName] = source;
        }

        public Source GetSource(string uniqueName) {
            return paramSources.TryGetValue(uniqueName, out var output)
                ? output
                : new Source { objectPath = "__global", offset = 0, originalParamName = uniqueName };
        }

        [Serializable]
        public struct Source {
            public string objectPath;
            public string originalParamName;
            public int offset;
        }
    }
}
