using System;
using JetBrains.Annotations;

namespace VF.Injector {
    [AttributeUsage(AttributeTargets.Field)]
    [MeansImplicitUse]
    internal class VFAutowiredAttribute : Attribute {
        
    }
}
