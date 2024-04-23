using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    /**
     * Handles creating the DirectTree for properties that need correction when scaling the avatar
     */
    [VFService]
    public class ScalePropertyCompensationService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly ScaleFactorService scaleFactorService;
        [VFAutowired] private readonly DirectBlendTreeService directTree;

        public void AddScaledProp(VFGameObject scaleReference, IEnumerable<(VFGameObject obj, Type ComponentType, string PropertyName, float InitialValue)> properties) {
            var scaleFactor = scaleFactorService.Get(scaleReference);
            if (scaleFactor == null) {
                return;
            }

            var zeroClip = manager.GetFx().NewClip($"scaleComp_{scaleReference.name}_zero");
            directTree.Add(zeroClip);

            var scaleClip = manager.GetFx().NewClip($"scaleComp_{scaleReference.name}_one");
            directTree.Add(scaleFactor, scaleClip);

            foreach (var prop in properties) {
                var objectPath = prop.obj.GetPath(manager.AvatarObject);
                scaleClip.SetCurve(
                    EditorCurveBinding.FloatCurve(objectPath, prop.ComponentType, prop.PropertyName),
                    prop.InitialValue
                );
                zeroClip.SetCurve(
                    EditorCurveBinding.FloatCurve(objectPath, prop.ComponentType, prop.PropertyName),
                    0
                );
            }
        }
    }
}
