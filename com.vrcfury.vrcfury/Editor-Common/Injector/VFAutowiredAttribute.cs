using System;
using JetBrains.Annotations;

namespace VF.Injector {
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Constructor)]
    [MeansImplicitUse(ImplicitUseKindFlags.Assign)]
    internal class VFAutowiredAttribute : Attribute {
        
    }
}
