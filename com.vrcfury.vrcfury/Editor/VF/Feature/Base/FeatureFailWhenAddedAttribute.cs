using System;

namespace VF.Feature.Base {
    [AttributeUsage(AttributeTargets.Class)]
    internal class FeatureFailWhenAddedAttribute : Attribute {
        private string message;

        public FeatureFailWhenAddedAttribute(string message) {
            this.message = message;
        }

        public string Message => message;
    }
}
