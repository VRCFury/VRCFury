using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;

namespace VF.Feature {
    [FeatureTitle("Security Restricted")]
    internal class SecurityRestrictedBuilder : FeatureBuilder<SecurityRestricted> {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ObjectMoveService mover;
        [VFAutowired] private readonly DbtLayerService directTreeService;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly GlobalsService globals;
        
        [FeatureBuilderAction(FeatureOrder.SecurityRestricted)]
        public void Apply() {
            if (featureBaseObject == avatarObject) {
                throw new Exception("The root object of your avatar cannot be security restricted, sorry!");
            }
            
            var parent = featureBaseObject.parent;
            while (parent != null && parent != avatarObject) {
                if (parent.GetComponents<VRCFury>()
                    .SelectMany(vf => vf.GetAllFeatures())
                    .Any(f => f is SecurityRestricted)) {
                    // some parent is restricted, so we can skip this one and just let the parent handle it
                    return;
                }
                parent = parent.parent;
            }

            var security = globals.allBuildersInRun.OfType<SecurityLockBuilder>().FirstOrDefault();
            if (security == null) {
                Debug.LogWarning("Security pin not set, restriction disabled");
                return;
            }

            var wrapper = GameObjects.Create(
                $"Security Restriction for {featureBaseObject.name}",
                featureBaseObject.parent,
                featureBaseObject.parent);
            
            mover.Move(featureBaseObject, wrapper);

            wrapper.active = false;

            var clip = clipFactory.NewClip("Unlock");
            clip.SetEnabled(wrapper, true);
            var directTree = directTreeService.Create($"Security Restrict {featureBaseObject.name}");
            directTree.Add(security.GetEnabled().AsFloat(), clip);
        }

        [FeatureEditor]
        public static VisualElement Editor() {
            return VRCFuryEditorUtils.Info(
                "This object will be forcefully disabled until a Security Pin is entered in your avatar's menu." +
                "Note: You MUST have a Security Pin Number component on your avatar root with a pin number set, or this will not do anything!"
            );
        }
    }
}
