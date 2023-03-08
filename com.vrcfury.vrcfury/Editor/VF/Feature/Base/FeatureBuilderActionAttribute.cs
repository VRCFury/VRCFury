using System;

namespace VF.Feature.Base {
    [AttributeUsage(AttributeTargets.Method)]
    public class FeatureBuilderActionAttribute : Attribute, IComparable<FeatureBuilderActionAttribute> {
        public readonly int priority;

        public FeatureBuilderActionAttribute(FeatureOrder priority = FeatureOrder.Default) {
            this.priority = (int)priority;
        }

        public int CompareTo(FeatureBuilderActionAttribute other) {
            if (priority < other.priority) return -1;
            if (priority > other.priority) return 1;
            return 0;
        }
    }
}
