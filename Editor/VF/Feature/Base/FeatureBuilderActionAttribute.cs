using System;

namespace VF.Feature.Base {
    [AttributeUsage(AttributeTargets.Method)]
    public class FeatureBuilderActionAttribute : Attribute, IComparable<FeatureBuilderActionAttribute> {
        public readonly int priority;
        public readonly bool applyToVrcClone;

        public FeatureBuilderActionAttribute(int priority = 0, bool applyToVrcClone = false) {
            this.priority = priority;
            this.applyToVrcClone = applyToVrcClone;
        }

        public int CompareTo(FeatureBuilderActionAttribute other) {
            if (priority < other.priority) return -1;
            if (priority > other.priority) return 1;
            if (!applyToVrcClone && other.applyToVrcClone) return -1;
            if (applyToVrcClone && !other.applyToVrcClone) return 1;
            return 0;
        }
    }
}
