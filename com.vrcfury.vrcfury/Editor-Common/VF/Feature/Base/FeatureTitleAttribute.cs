using System;
using JetBrains.Annotations;

namespace VF.Feature.Base {
    [MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
    [AttributeUsage(AttributeTargets.Class)]
    internal class FeatureTitleAttribute : Attribute {
        public FeatureTitleAttribute(string title) {
            this.Title = title;
        }

        public string Title { get; }
    }
}
