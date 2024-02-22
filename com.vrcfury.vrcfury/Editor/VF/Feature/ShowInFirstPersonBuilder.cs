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

namespace VF.Feature {
    public class ShowInFirstPersonBuilder : FeatureBuilder<ShowInFirstPerson> {
        [VFAutowired] private readonly ObjectMoveService mover;
        [VFAutowired] private readonly FakeHeadService fakeHead;
        
        [FeatureBuilderAction(FeatureOrder.ShowInFirstPersonBuilder)]
        public void Apply() {
            var obj = model.useObjOverride ? model.objOverride.asVf() : featureBaseObject;
            if (obj == null) return;

            if (model.onlyIfChildOfHead) {
                var h = VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, HumanBodyBones.Head);
                if (h == null || !obj.IsChildOf(h)) return;
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
