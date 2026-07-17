using System;
using JetBrains.Annotations;

namespace com.vrcfury.udon.Attributes {
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    [MeansImplicitUse]
    public sealed class InjectUnityActionAttribute : Attribute {
        public readonly string actionName;

        public InjectUnityActionAttribute(string actionName) {
            this.actionName = actionName;
        }
    }
}
