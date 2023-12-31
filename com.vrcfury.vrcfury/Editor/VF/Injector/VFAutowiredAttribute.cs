using System;
using JetBrains.Annotations;

namespace VF.Injector {
    [AttributeUsage(AttributeTargets.Field)]
    [MeansImplicitUse]
    public class VFAutowiredAttribute : Attribute {
        
    }
}
