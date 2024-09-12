using System;

namespace VF.Feature.Base {
    [AttributeUsage(AttributeTargets.Class)]
    public class FeatureFailWhenAddedAttribute : Attribute {
        private string message;

        public FeatureFailWhenAddedAttribute(string message) {
            this.message = message;
        }

        public string Message => message;
    }
}
