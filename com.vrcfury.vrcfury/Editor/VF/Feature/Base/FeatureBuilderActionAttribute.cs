using System;

namespace VF.Feature.Base {
    [AttributeUsage(AttributeTargets.Method)]
    public class FeatureBuilderActionAttribute : Attribute, IComparable<FeatureBuilderActionAttribute> {
        private readonly int priority;
        private readonly int priorityOffset;

        /**
         * VRCFury does not support custom plugins!
         * However, if you happen to want to write your own unsupported custom plugin, you can insert
         * your own action at any point in the process by choosing one of the built in FeatureOrders, then
         * setting priorityOffset to -1 or 1 so it runs before or after that step. Beware that this is not
         * a supported API.
         */
        public FeatureBuilderActionAttribute(FeatureOrder priority = FeatureOrder.Default, int priorityOffset = 0) {
            this.priority = (int)priority;
            this.priorityOffset = priorityOffset;
        }

        public int CompareTo(FeatureBuilderActionAttribute other) {
            return (priority, priorityOffset).CompareTo(
                (other.priority, other.priorityOffset));
        }
    }
}
