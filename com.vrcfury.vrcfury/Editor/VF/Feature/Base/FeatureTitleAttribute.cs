using System;
using JetBrains.Annotations;

namespace VF.Feature.Base {
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Class)]
    internal class FeatureTitleAttribute : Attribute {
        public FeatureTitleAttribute(string title) {
            this.Title = title;
        }

        public string Title { get; }
    }
}
