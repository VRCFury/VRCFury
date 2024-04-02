using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    public class ShowInFirstPersonBuilder : FeatureBuilder<ShowInFirstPerson> {
        [VFAutowired] private readonly ObjectMoveService mover;
        [VFAutowired] private readonly FakeHeadService fakeHead;
        
        [FeatureBuilderAction(FeatureOrder.ShowInFirstPersonBuilder)]
        public void Apply() {
            var obj = model.useObjOverride ? model.objOverride.asVf() : featureBaseObject;
            if (obj == null) return;

            var head = VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, HumanBodyBones.Head);
            if (head == null) return;

            var isChildOfHead = obj.IsChildOf(head);
            if (model.onlyIfChildOfHead && !isChildOfHead) return;

#if VRCSDK_HAS_HEAD_CHOP
            if (!isChildOfHead) {
                addOtherFeature(new ArmatureLink {
                    propBone = obj,
                    linkTo = new List<ArmatureLink.LinkTo> {
                        new ArmatureLink.LinkTo {
                            obj = head,
                            useBone = false,
                            useObj = true
                        }
                    },
                    linkMode = ArmatureLink.ArmatureLinkMode.ReparentRoot
                });
            }
            
            var headChopObj = fakeHead.GetHeadChopObj();
            var headChop = headChopObj.GetComponents<VRCHeadChop>().FirstOrDefault(c => c.targetBones.Length < 32);
            if (headChop == null) headChop = headChopObj.AddComponent<VRCHeadChop>();
            headChop.targetBones = headChop.targetBones.Append(new VRCHeadChop.HeadChopBone() {
                transform = obj,
                scaleFactor = 1,
                applyCondition = VRCHeadChop.HeadChopBone.ApplyCondition.AlwaysApply
            }).ToArray();
#else
            if (obj.parent != head) {
                throw new Exception(
                    "You are using a VRCFury feature that requires VRCSDK HeadChop, but your VRCSDK is too old. Please update your VRCSDK to the newest, or at least version 3.5.2."
                );
            }
            addOtherFeature(new ArmatureLink {
                propBone = obj,
                linkTo = new List<ArmatureLink.LinkTo> {
                    new ArmatureLink.LinkTo {
                        obj = fakeHead.GetFakeHead(),
                        useBone = false,
                        useObj = true
                    }
                },
                linkMode = ArmatureLink.ArmatureLinkMode.ReparentRoot
            });
#endif
        }

        public override string GetEditorTitle() {
            return "Show In First Person";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            return VRCFuryEditorUtils.Info(
                "This component will automatically make this GameObject a child of the head bone, and will" +
                " use constraint tricks to make it visible in first person.\n\n" +
                "Warning:\n" +
                "* Do not combine this with armature link.\n" +
                "* First person objects cannot be attached to non-first-person" +
                " objects, such as nose or ear bones.\n" +
                "* World position will be maintained when reparented.");
        }
    }
}
