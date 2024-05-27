using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    /** This builder is responsible for creating a fake head bone, and moving
     * objects onto it, if those objects should be visible in first person.
     */
    [VFService]
    public class FakeHeadService {

        [VFAutowired] private readonly AvatarManager manager;

        private readonly Lazy<VFGameObject> fakeHead;
        private readonly Lazy<VFGameObject> headChopObj;
        public FakeHeadService() {
            fakeHead = new Lazy<VFGameObject>(MakeFakeHead);
            headChopObj = new Lazy<VFGameObject>(() => {
                var head = VRCFArmatureUtils.FindBoneOnArmatureOrException(manager.AvatarObject, HumanBodyBones.Head);
                return GameObjects.Create("vrcfHeadChop", head);
            });
        }

        public VFGameObject GetFakeHead() {
            return fakeHead.Value;
        }

        public VFGameObject GetHeadChopObj() {
            return headChopObj.Value;
        }

        private VFGameObject MakeFakeHead() {
            var head = VRCFArmatureUtils.FindBoneOnArmatureOrException(manager.AvatarObject, HumanBodyBones.Head);
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) {
                return head;
            }

            var vrcfAlwaysVisibleHead = GameObjects.Create("vrcfAlwaysVisibleHead", head.parent, useTransformFrom: head);
            
            var p = vrcfAlwaysVisibleHead.AddComponent<ParentConstraint>();
            p.AddSource(new ConstraintSource() {
                sourceTransform = head,
                weight = 1
            });
            p.weight = 1;
            p.constraintActive = true;
            p.locked = true;

            return vrcfAlwaysVisibleHead;
        }
    }
}
