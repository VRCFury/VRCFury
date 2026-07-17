using System;
using JetBrains.Annotations;

namespace VF.Injector {
    [AttributeUsage(AttributeTargets.Class)]
    [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
    internal class VFServiceAttribute : Attribute {
        
    }
}
