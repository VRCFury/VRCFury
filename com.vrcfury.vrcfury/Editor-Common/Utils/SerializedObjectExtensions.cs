using JetBrains.Annotations;
using UnityEditor;
using VF.Builder;
using VF.Model;
using VF.Model.Feature;

namespace VF.Utils {
    internal static class SerializedObjectExtensions {
        [CanBeNull]
        public static UnityEngine.Component GetComponent(this SerializedObject obj) {
            return (obj.targetObject as UnityEngine.Component).NullSafe();
        }
        
        [CanBeNull]
        public static VFGameObject GetGameObject(this SerializedObject obj) {
            return obj.GetComponent()?.owner();
        }

        [CanBeNull]
        public static FeatureModel GetVrcFuryFeature(this SerializedObject obj) {
            return (obj.GetComponent() as VRCFury)?.content;
        }
    }
}
