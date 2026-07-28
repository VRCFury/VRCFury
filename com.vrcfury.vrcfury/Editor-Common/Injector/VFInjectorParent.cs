using System;
using System.Collections.Generic;

namespace VF.Injector {
    internal class VFInjectorParent {
        public object parent;
        internal readonly Dictionary<Type, object> scopedServices = new Dictionary<Type, object>();
    }
}
