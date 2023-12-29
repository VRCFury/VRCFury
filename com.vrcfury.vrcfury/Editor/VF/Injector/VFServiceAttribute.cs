using System;
using JetBrains.Annotations;

namespace VF.Injector {
    [AttributeUsage(AttributeTargets.Class)]
    [MeansImplicitUse]
    public class VFServiceAttribute : Attribute {
        
    }
}
