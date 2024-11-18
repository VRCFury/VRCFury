using System.Collections.Generic;
using VF.Builder;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * Creates a physbone resetter that can be triggered by triggering the returned bool
     */
    [VFService]
    internal class PhysboneResetService {
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly DbtLayerService dbtLayerService;
        [VFAutowired] private readonly ClipFactoryService clipFactory;

        public VFAFloat CreatePhysBoneResetter(VFGameObject physBone, string name) {
            var directTree = dbtLayerService.Create(physBone.name);
            var blendtreeMath = dbtLayerService.GetMath(directTree);
            
            var param = fx.NewFloat(name + "_PhysBoneReset");
            var buffer1 = blendtreeMath.Buffer(param);
            var buffer2 = blendtreeMath.Buffer(buffer1);
            var buffer3 = blendtreeMath.Buffer(buffer2);
            
            var resetClip = clipFactory.NewClip("Physbone Reset");
            resetClip.SetEnabled(physBone, false);

            directTree.Add(BlendtreeMath.Xor(BlendtreeMath.GreaterThan(buffer1, 0), BlendtreeMath.GreaterThan(buffer3, 0))
                .create(resetClip, clipFactory.GetEmptyClip()));

            return param;
        }
    }
}
