using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VRC.Dynamics;
#if VRCSDK_HAS_VRCCONSTRAINTS
using VRC.SDK3.Dynamics.Constraint.Components;
#endif

namespace VF.Utils {
    internal class VFConstraint {
        private readonly UnityEngine.Component component;
        
        private VFConstraint(UnityEngine.Component component) {
            this.component = component;
        }

        [CanBeNull]
        public static VFConstraint CreateOrNull(UnityEngine.Component component) {
            if (component == null) return null;
            if (component is IConstraint) return new VFConstraint(component);
#if VRCSDK_HAS_VRCCONSTRAINTS
            if (component is VRCConstraintBase) return new VFConstraint(component);
#endif
            return null;
        }

        public VFGameObject GetAffectedObject() {
#if VRCSDK_HAS_VRCCONSTRAINTS
            if (component is VRCConstraintBase vrcConstraint) {
                return vrcConstraint.GetEffectiveTargetTransform();
            }
#endif
            return component.owner();
        }
        
        [CanBeNull]
        public VFGameObject GetFirstSource() {
#if VRCSDK_HAS_VRCCONSTRAINTS
            if (component is VRCConstraintBase vrcConstraint) {
                if (vrcConstraint.Sources.Count == 0) return null;
                var source = vrcConstraint.Sources.First().SourceTransform;
                if (source == null) return null;
                return source;
            }
#endif

            if (component is IConstraint unityConstraint) {
                if (unityConstraint.sourceCount == 0) return null;
                var source = unityConstraint.GetSource(0).sourceTransform;
                if (source == null) return null;
                return source;
            }

            return null;
        }

        public bool IsScale() {
#if VRCSDK_HAS_VRCCONSTRAINTS
            if (component is VRCScaleConstraint) return true;
#endif
            return component is ScaleConstraint;
        }

        public bool IsParent() {
#if VRCSDK_HAS_VRCCONSTRAINTS
            if (component is VRCParentConstraint) return true;
#endif
            return component is ParentConstraint;
        }

        public bool IsPosition() {
#if VRCSDK_HAS_VRCCONSTRAINTS
            if (component is VRCPositionConstraint) return true;
#endif
            return component is PositionConstraint;
        }

        public VFGameObject[] GetSources() {
#if VRCSDK_HAS_VRCCONSTRAINTS
            if (component is VRCConstraintBase vrcConstraint) {
                return vrcConstraint.Sources.Select(s => s.SourceTransform.asVf()).ToArray();
            }
#endif
            if (component is IConstraint unityConstraint) {
                return unityConstraint.GetSources().Select(s => s.sourceTransform.asVf()).ToArray();
            }
            return new VFGameObject[] { };
        }

        public float GetWeight(int i) {
#if VRCSDK_HAS_VRCCONSTRAINTS
            if (component is VRCConstraintBase vrcConstraint) return vrcConstraint.Sources[i].Weight;
#endif
            if (component is IConstraint unityConstraint) return unityConstraint.GetSource(i).weight;
            return 0;
        }
        
        public Vector3 GetPositionOffset(int i) {
#if VRCSDK_HAS_VRCCONSTRAINTS
            if (component is VRCConstraintBase vrcConstraint) return vrcConstraint.Sources[i].ParentPositionOffset;
#endif
            if (component is ParentConstraint unityConstraint) return unityConstraint.GetTranslationOffset(i);
            return Vector3.zero;
        }
        
        public Vector3 GetRotationOffset(int i) {
#if VRCSDK_HAS_VRCCONSTRAINTS
            if (component is VRCConstraintBase vrcConstraint) return vrcConstraint.Sources[i].ParentRotationOffset;
#endif
            if (component is ParentConstraint unityConstraint) return unityConstraint.GetRotationOffset(i);
            return Vector3.zero;
        }

        public UnityEngine.Component GetComponent() {
            return component;
        }

        public void Destroy() {
            Object.DestroyImmediate(component);
        }

        public VFGameObject owner() {
            return component.owner();
        }
    }
}
