using System.Collections.Generic;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * Creates a physbone resetter that can be triggered by triggering the returned bool
     */
    [VFService]
    public class PhysboneResetService {
        [VFAutowired] private AvatarManager avatarManager;
        [VFAutowired] private ClipBuilderService clipBuilder;
        
        public VFABool CreatePhysBoneResetter(List<GameObject> resetPhysbones, string name) {
            if (resetPhysbones == null || resetPhysbones.Count == 0) return null;

            var fx = avatarManager.GetFx();
            var layer = fx.NewLayer(name + " (PhysBone Reset)");
            var param = fx.NewTrigger(name + "_PhysBoneReset");
            var idle = layer.NewState("Idle");
            var pause = layer.NewState("Pause");
            var reset1 = layer.NewState("Reset").Move(pause, 1, 0);
            var reset2 = layer.NewState("Reset").Move(idle, 1, 0);
            idle.TransitionsTo(pause).When(param.IsTrue());
            pause.TransitionsTo(reset1).When(fx.Always());
            reset1.TransitionsTo(reset2).When(fx.Always());
            reset2.TransitionsTo(idle).When(fx.Always());

            var resetClip = fx.NewClip("Physbone Reset");
            foreach (var physBone in resetPhysbones) {
                if (physBone == null) {
                    Debug.LogWarning("Physbone object in physboneResetter is missing!: " + name);
                    continue;
                }
                clipBuilder.Enable(resetClip, physBone, false);
            }

            reset1.WithAnimation(resetClip);
            reset2.WithAnimation(resetClip);

            return param;
        }
    }
}