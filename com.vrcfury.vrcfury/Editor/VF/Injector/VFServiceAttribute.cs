using System;
using JetBrains.Annotations;

namespace VF.Injector {
    [AttributeUsage(AttributeTargets.Class)]
    [MeansImplicitUse]
    internal class VFServiceAttribute : Attribute {
        
    }
}
