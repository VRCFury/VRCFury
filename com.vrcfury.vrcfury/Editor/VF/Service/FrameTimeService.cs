﻿using UnityEditor;
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
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly DbtLayerService dbtLayerService;

        private VFAFloat cachedFrameTime;
        public VFAFloat GetFrameTime() {
            if (cachedFrameTime != null) return cachedFrameTime;

            var timeSinceLoad = GetTimeSinceLoad();
            var dbt = dbtLayerService.Create();
            var math = dbtLayerService.GetMath(dbt);
            var lastTimeSinceLoad = math.Buffer(timeSinceLoad, to: "lastTimeSinceLoad", def: 1f/60);
            var diff = math.Subtract(timeSinceLoad, lastTimeSinceLoad, name: "frameTime");

            cachedFrameTime = diff;
            return diff;
        }

        private VFAFloat cachedFps;
        public VFAFloat GetFps() {
            if (cachedFps != null) return cachedFps;
            var frametime = GetFrameTime();
            var dbt = dbtLayerService.Create();
            var math = dbtLayerService.GetMath(dbt);
            return cachedFps = math.Invert("fps", frametime);
        }

        private VFAFloat cachedLoadTime;
        public VFAFloat GetTimeSinceLoad() {
            if (cachedLoadTime != null) return cachedLoadTime;

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
