using System.Collections.Generic;
using VF.Injector;

namespace VF.Service {
    [VFService]
    public class UniqueHapticNamesService {
        private List<string> usedNames = new List<string>();

        public string GetUniqueName(string prefix) {
            for (int i = 0; ; i++) {
                var next = prefix + (i == 0 ? "" : i+"");
                if (!usedNames.Contains(next)) {
                    usedNames.Add(next);
                    return next;
                }
            }
        }
    }
}
