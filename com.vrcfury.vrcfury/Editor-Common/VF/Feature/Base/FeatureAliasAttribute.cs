using System;

namespace VF.Feature.Base {
    [AttributeUsage(AttributeTargets.Class)]
    internal class FeatureAliasAttribute : Attribute {
        private readonly string oldTitle;

        public FeatureAliasAttribute(string oldTitle) {
            this.oldTitle = oldTitle;
        }

        public string OldTitle => oldTitle;
    }
}
