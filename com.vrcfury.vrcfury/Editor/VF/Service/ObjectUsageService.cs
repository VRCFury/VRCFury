using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Utils;

namespace VF.Service {
    public class ObjectUsageService {
        public static VFMultimapSet<VFGameObject,string> GetUsageReasons(VFGameObject avatarObject) {
            var reasons = new VFMultimapSet<VFGameObject,string>();

            foreach (var component in avatarObject.GetComponentsInSelfAndChildren<UnityEngine.Component>()) {
                if (component is Transform) continue;
                reasons.Put(component.owner(), "Contains components");

                var so = new SerializedObject(component);
                var prop = so.GetIterator();
                do {
                    if (prop.propertyPath.StartsWith("ignoreTransforms.Array")) {
                        // TODO: If we remove objects that are in these physbone ignoreTransforms arrays, we should
                        // probably also remove them from the array instead of just leaving it null
                        continue;
                    }
                    if (prop.propertyType == SerializedPropertyType.ObjectReference) {
                        VFGameObject target = null;
                        if (prop.objectReferenceValue is Transform t) target = t;
                        else if (prop.objectReferenceValue is GameObject g) target = g;
                        if (target != null && target.IsChildOf(avatarObject)) {
                            reasons.Put(target, prop.propertyPath + " in " + component.GetType().Name + " on " + component.owner().GetPath(avatarObject, true));
                        }
                    }
                } while (prop.Next(true));
            }

            foreach (var used in reasons.GetKeys().ToArray()) {
                foreach (var parent in used.GetSelfAndAllParents()) {
                    if (parent != used && parent.IsChildOf(avatarObject)) {
                        reasons.Put(parent, "A child object is used");
                    }
                }
            }

            return reasons;
        }
    }
}
