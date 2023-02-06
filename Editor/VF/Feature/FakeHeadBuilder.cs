using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Feature.Base;

namespace VF.Feature {
    /** This builder is responsible for creating a fake head bone, and moving
     * objects onto it, if those objects should be visible in first person.
     */
    public class FakeHeadBuilder : FeatureBuilder {

        private HashSet<GameObject> objectsEligibleForFakeHead = new HashSet<GameObject>();

        public void MarkEligible(GameObject obj) {
            objectsEligibleForFakeHead.Add(obj);
        }

        [FeatureBuilderAction(FeatureOrder.FakeHeadBuilder)]
        public void Apply() {
#if UNITY_ANDROID
            return;
#endif

            var head = VRCFArmatureUtils.FindBoneOnArmature(avatarObject, HumanBodyBones.Head);
            if (!head) return;
            
            var objectsForFakeHead = objectsEligibleForFakeHead
                .Where(obj => obj.transform.parent == head.transform)
                .ToList();
            if (objectsForFakeHead.Count == 0) return;
            
            var mover = allBuildersInRun.OfType<ObjectMoveBuilder>().First();
            var vrcfAlwaysVisibleHead = new GameObject("vrcfAlwaysVisibleHead");
            vrcfAlwaysVisibleHead.transform.SetParent(head.transform, false);
            vrcfAlwaysVisibleHead.transform.SetParent(head.transform.parent, true);
            vrcfAlwaysVisibleHead.transform.localScale = head.transform.localScale;
            
            var p = vrcfAlwaysVisibleHead.AddComponent<ParentConstraint>();
            p.AddSource(new ConstraintSource() {
                sourceTransform = head.transform,
                weight = 1
            });
            p.weight = 1;
            p.constraintActive = true;
            p.locked = true;

            foreach (var obj in objectsForFakeHead) {
                mover.MoveToParent(obj, vrcfAlwaysVisibleHead);
            }
        }
    }
}
