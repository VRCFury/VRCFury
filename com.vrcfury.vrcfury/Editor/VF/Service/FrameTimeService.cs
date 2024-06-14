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
    internal class FrameTimeService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly DirectBlendTreeService directTree;
        [VFAutowired] private readonly ClipFactoryService clipFactory;

        private VFAFloat cachedFrameTime;
        public VFAFloat GetFrameTime() {
            if (cachedFrameTime != null) return cachedFrameTime;

            var timeSinceLoad = GetTimeSinceLoad();
            var lastTimeSinceLoad = math.Buffer(timeSinceLoad, to: "lastTimeSinceLoad");
            var diff = math.Subtract(timeSinceLoad, lastTimeSinceLoad, name: "frameTime");

            cachedFrameTime = diff;
            return diff;
        }

        private VFAFloat cachedLoadTime;
        public VFAFloat GetTimeSinceLoad() {
            if (cachedLoadTime != null) return cachedLoadTime;

            var fx = manager.GetFx();
            var timeSinceStart = fx.NewFloat("timeSinceLoad");
            var layer = fx.NewLayer("FrameTime Counter");
            var clip = clipFactory.NewClip("FrameTime Counter");
            clip.SetAap(
                timeSinceStart,
                AnimationCurve.Linear(0, 0, 10_000_000, 10_000_000)
            );
            layer.NewState("Time").WithAnimation(clip);

            cachedLoadTime = timeSinceStart;
            return timeSinceStart;
        }
    }
}
