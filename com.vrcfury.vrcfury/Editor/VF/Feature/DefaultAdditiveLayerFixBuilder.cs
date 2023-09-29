using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Feature.Base;
using VRC.SDK3.Avatars.Components;

namespace VF.Feature {
    /**
     * The default additive playable layer is a major contributor to the "3x unity blendshape" bug.
     * Simply removing it whenever it's set to the default goes a long way to resolving the issue.
     */
    public class DefaultAdditiveLayerFixBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.RemoveDefaultedAdditiveLayer)]
        public void Apply() {
            var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
            foreach (var c in VRCAvatarUtils.GetAllControllers(avatar)) {
                if (c.type == VRCAvatarDescriptor.AnimLayerType.Additive && c.isDefault) {
                    c.set(null);
                }
            }
        }
    }
}
