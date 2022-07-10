using System;
using System.Reflection;

namespace VF.Feature.Base {
    public class FeatureBuilderAction : IComparable<FeatureBuilderAction> {
        private FeatureBuilderActionAttribute attribute;
        private MethodInfo method;
        private FeatureBuilder builder;

        public FeatureBuilderAction(FeatureBuilderActionAttribute attribute, MethodInfo method, FeatureBuilder builder) {
            this.attribute = attribute;
            this.method = method;
            this.builder = builder;
        }

        public void Call() {
            method.Invoke(builder, new object[] { });
        }

        public bool applyToVrcClone() {
            return attribute.applyToVrcClone;
        }

        public int CompareTo(FeatureBuilderAction other) {
            return attribute.CompareTo(other.attribute);
        }

        public FeatureBuilder GetBuilder() {
            return builder;
        }

        public string GetName() {
            return builder.GetType().Name + "." + method.Name;
        }
    }
}
