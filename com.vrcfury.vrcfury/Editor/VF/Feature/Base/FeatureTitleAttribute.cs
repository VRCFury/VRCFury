using System;

namespace VF.Feature.Base {
    [AttributeUsage(AttributeTargets.Class)]
    internal class FeatureTitleAttribute : Attribute {
        public FeatureTitleAttribute(string title) {
            this.Title = title;
        }

        public string Title { get; }
    }
}
