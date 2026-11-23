using System;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Exceptions;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Service {
    /**
     * Applies the armature cache's validated Hips correction to the build Animator.
     */
    [VFService]
    internal class FixHumanoidHipsService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly VRCFArmatureCache armatureCache;

        [FeatureBuilderAction(FeatureOrder.FixHumanoidHips)]
        public void Apply() {
            var animator = avatarObject.GetComponent<Animator>();
            var source = animator == null ? null : animator.avatar;
            if (source == null) return;

            var correction = armatureCache.GetHipsCorrection();
            if (correction == null) return;

            Avatar repaired = null;
            try {
                repaired = correction.CreateAvatar(source);
                VrcfObjectFactory.Register(repaired, source);
                // The Animator.avatar setter can reset the current pose when changing the rig.
                using var serialized = new SerializedObject(animator);
                serialized.FindProperty("m_Avatar").objectReferenceValue = repaired;
                if (!serialized.ApplyModifiedPropertiesWithoutUndo() || animator.avatar != repaired) {
                    throw new VRCFBuilderException("Unity rejected the corrected humanoid Avatar on the Animator.");
                }
            } catch (Exception e) {
                if (repaired != null) Object.DestroyImmediate(repaired);
                throw new Exception(correction.FailureMessage, e);
            }
        }
    }
}
