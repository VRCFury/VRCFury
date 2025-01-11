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
    [FeatureTitle("Show In First Person")]
    internal class ShowInFirstPersonBuilder : FeatureBuilder<ShowInFirstPerson> {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly FakeHeadService fakeHead;
        [VFAutowired] private readonly GlobalsService globals;
        
        [FeatureBuilderAction]
        public void Apply() {
            var obj = model.useObjOverride ? model.objOverride.asVf() : featureBaseObject;
            if (obj == null) return;

            var head = VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, HumanBodyBones.Head);
            if (head == null) return;

            globals.addOtherFeature(new ArmatureLink {
                propBone = obj,
                linkTo = new List<ArmatureLink.LinkTo> {
                    new ArmatureLink.LinkTo {
#if VRCSDK_HAS_HEAD_CHOP
                        obj = head,
#else
                        obj = fakeHead.GetFakeHead(),
#endif
                        useBone = false,
                        useObj = true
                    }
                },
                recursive = false,
                onlyIf = () => {
                    var isChildOfHead = obj.IsChildOf(head);
                    if (model.onlyIfChildOfHead && !isChildOfHead) return false;

#if VRCSDK_HAS_HEAD_CHOP
                    var headChopObj = fakeHead.GetHeadChopObj();
                    var headChop = headChopObj.GetComponents<VRCHeadChop>().FirstOrDefault(c => c.targetBones.Length < 32);
                    if (headChop == null) headChop = headChopObj.AddComponent<VRCHeadChop>();
                    headChop.targetBones = headChop.targetBones.Append(new VRCHeadChop.HeadChopBone() {
                        transform = obj,
                        scaleFactor = 1,
                        applyCondition = VRCHeadChop.HeadChopBone.ApplyCondition.AlwaysApply
                    }).ToArray();
                    if (isChildOfHead) return false;
#endif

                    return true;
                }
            });
        }

        [FeatureEditor]
        public static VisualElement Editor() {
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
