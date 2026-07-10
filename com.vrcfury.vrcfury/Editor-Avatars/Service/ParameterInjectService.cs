using System.Collections.Generic;
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
    }
}
