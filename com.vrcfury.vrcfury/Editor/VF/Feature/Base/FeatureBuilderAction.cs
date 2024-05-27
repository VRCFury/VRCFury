using System;
using System.Reflection;
using JetBrains.Annotations;
using VF.Builder;

namespace VF.Feature.Base {
    public class FeatureBuilderAction : IComparable<FeatureBuilderAction> {
        private readonly FeatureBuilderActionAttribute attribute;
        private readonly MethodInfo method;
        private readonly object service;
        public int serviceNum { get; private set; }
        public VFGameObject configObject { get; private set; }

        public FeatureBuilderAction(FeatureBuilderActionAttribute attribute, MethodInfo method, object service, int serviceNum, VFGameObject configObject) {
            this.attribute = attribute;
            this.method = method;
            this.service = service;
            this.serviceNum = serviceNum;
            this.configObject = configObject;
        }

        public void Call() {
            method.Invoke(service, new object[] { });
        }

        public int CompareTo(FeatureBuilderAction other) {
            return (
                (int)attribute.GetPriority(),
                attribute.GetPriorityOffset(),
                serviceNum
            ).CompareTo((
                (int)other.attribute.GetPriority(),
                other.attribute.GetPriorityOffset(),
                serviceNum
            ));
        }

        public object GetService() {
            return service;
        }

        public string GetName() {
            return method.Name;
        }

        public FeatureOrder GetPriorty() => attribute.GetPriority();
    }
}
