using System;
using JetBrains.Annotations;

namespace VF.Injector {
    [AttributeUsage(AttributeTargets.Field)]
    [MeansImplicitUse(ImplicitUseKindFlags.Assign)]
    internal class VFAutowiredAttribute : Attribute {
        
    }
}
