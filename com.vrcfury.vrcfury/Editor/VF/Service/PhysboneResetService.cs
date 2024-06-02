using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * Creates a physbone resetter that can be triggered by triggering the returned bool
     */
    [VFService]
    internal class PhysboneResetService {
        [VFAutowired] private AvatarManager avatarManager;
        [VFAutowired] private ClipBuilderService clipBuilder;
        [VFAutowired] private MathService mathService;
        [VFAutowired] private DirectBlendTreeService directTree;
        [VFAutowired] private readonly ClipFactoryService clipFactory;

        public VFAFloat CreatePhysBoneResetter(ICollection<VFGameObject> resetPhysbones, string name) {
            var fx = avatarManager.GetFx();
            var param = fx.NewFloat(name + "_PhysBoneReset");
            var buffer1 = mathService.Buffer(param);
            var buffer2 = mathService.Buffer(buffer1);
            var buffer3 = mathService.Buffer(buffer2);
            
            var resetClip = clipFactory.NewClip("Physbone Reset");
            foreach (var physBone in resetPhysbones) {
                clipBuilder.Enable(resetClip, physBone, false);
            }

            directTree.Add(mathService.Xor(mathService.GreaterThan(buffer1, 0), mathService.GreaterThan(buffer3, 0))
                .create(resetClip, clipFactory.GetEmptyClip()));

            return param;
        }
    }
}
