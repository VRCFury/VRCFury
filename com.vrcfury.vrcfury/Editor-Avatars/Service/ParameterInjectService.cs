using System;
using System.Collections.Generic;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    [VFService]
    internal class ParameterInjectService {
        public class Request {
            public VFGameObject sourceObject;
            public string sourceParam;
            public string resolvedParam;
        }

        private readonly List<Request> requests = new List<Request>();

        public void Register(Request request) {
            requests.Add(request);
        }

        public IEnumerable<Request> GetRequests() {
            return requests;
        }

        public void MarkResolved(Request request) {
            requests.Remove(request);
        }

        [FeatureBuilderAction(FeatureOrder.HapticsAnimationRewrites)]
        public void Validate() {
            if (!BuildTargetUtils.IsDesktop()) return;
            foreach (var request in requests) {
                throw new Exception($"Cannot inject parameter '{request.sourceParam}' from "
                    + $"'{request.sourceObject?.GetDebugPath() ?? "(missing object)"}' into '{request.resolvedParam}': "
                    + "the source does not support this parameter.");
            }
        }
    }
}
