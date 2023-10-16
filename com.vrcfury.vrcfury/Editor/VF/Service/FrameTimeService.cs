using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * This service gives you the current frametime. Woo!
     */
    [VFService]
    public class FrameTimeService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        private VFAFloat cached;

        public VFAFloat GetFrameTime() {
            if (cached != null) return cached;

            var fx = manager.GetFx();
            var next = fx.NewFloat("frameTimeNext");
            var previous = fx.NewFloat("frameTimePrev");
            directTree.Add(math.MakeCopier(next, previous));
            
            var clip = fx.NewClip("FrameTimeCounter");
            clip.SetCurve(
                EditorCurveBinding.FloatCurve("", typeof(Animator), next.Name()),
                new FloatOrObjectCurve(AnimationCurve.Linear(0, 0, 10_000_000, 10_000_000))
            );
            directTree.Add(clip);

            var diff = math.Subtract("frameTime", next, previous);

            cached = diff;
            return diff;
        }
    }
}
