using System;
using JetBrains.Annotations;

namespace VF.Feature.Base {
    [AttributeUsage(AttributeTargets.Method)]
    [MeansImplicitUse]
    internal class FeatureEditorAttribute : Attribute {
    }
}
