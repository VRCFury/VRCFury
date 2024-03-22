using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using static VF.Utils.VRCExpressionsMenuExtensions.ForEachMenuItemResult;

namespace Editor.VF.Feature {
    public class UnlimitedParametersBuilder : FeatureBuilder<UnlimitedParameters> {
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly DirectBlendTreeService directTree;

        private static readonly FieldInfo networkSyncedField =
            typeof(VRCExpressionParameters.Parameter).GetField("networkSynced");

        [FeatureBuilderAction(FeatureOrder.UnlimitedParameters)]
        public void Apply() {
            if (networkSyncedField == null) {
                throw new Exception("Your VRCSDK is too old to support the Unlimited Parameters component.");
            }

            var floatsToOptimize = GetFloatsToOptimize();
            if (floatsToOptimize.Count <= 2) return; // don't optimize 16 bits or less
            if (floatsToOptimize.Count > 255) throw new Exception("You have more than 255 floats? o.o");

            foreach (var param in floatsToOptimize) {
                var vrcPrm = manager.GetParams().GetParam(param.Name());
                networkSyncedField.SetValue(vrcPrm, false);
            }

            var syncPointer = fx.NewInt("SyncPointer", synced: true);
            var syncData = fx.NewFloat("SyncData", synced: true);

            var layer = fx.NewLayer("Unlimited Parameters");
            var entry = layer.NewState("Entry");
            var local = layer.NewState("Local");
            entry.TransitionsTo(local).When(fx.IsLocal().IsTrue());

            Action addRoundRobins = () => { };
            Action addDefault = () => { };
            for (int i = 0; i < floatsToOptimize.Count; i++) {
                var src = floatsToOptimize[i];
                var lastValue = fx.NewFloat($"{src.Name()}/LastSynced", def: -100);
                var diff = math.Subtract(src, lastValue);

                var sendState = layer.NewState($"Send {floatsToOptimize[i].Name()}");
                if (i == 0) sendState.Move(local, 1, 0);
                sendState
                    .DrivesCopy(syncData, src)
                    .DrivesCopy(lastValue, src)
                    .Drives(syncPointer, i)
                    .TransitionsTo(local)
                    .WithTransitionExitTime(0.1f)
                    .When(fx.Always());
                local.TransitionsTo(sendState)
                    .When(diff.AsFloat().IsLessThan(0).Or(diff.AsFloat().IsGreaterThan(0)));
                if (i == 0) {
                    addDefault = () => {
                        local.TransitionsTo(sendState).When(fx.Always());
                    };
                } else {
                    var fromI = i - 1; // Needs to be set outside the lambda
                    addRoundRobins += () => {
                        local.TransitionsTo(sendState).When(syncPointer.IsEqualTo(fromI));
                    };
                }
            }
            addRoundRobins();
            addDefault();

            // Receive
            var remote = layer.NewState("Remote").Move(local, 2, 0);
            entry.TransitionsTo(remote).When(fx.Always());
            for (int i = 0; i < floatsToOptimize.Count; i++) {
                var dst = floatsToOptimize[i];
                var receiveState = layer.NewState($"Receive {floatsToOptimize[i].Name()}");
                if (i == 0) receiveState.Move(remote, 1, 0);
                receiveState
                    .DrivesCopy(dst, syncData)
                    .TransitionsTo(remote)
                    .When(fx.Always());
                remote.TransitionsTo(receiveState).When(syncPointer.IsEqualTo(i));
            }

            Debug.Log($"Radial Toggle Optimizer: Reduced {floatsToOptimize.Count * 8} bits into 16 bits.");
        }

        private List<VFAFloat> GetFloatsToOptimize() {
            var drivenParams = manager.GetAllUsedControllers()
                .Select(c => c.GetRaw().GetRaw())
                .SelectMany(controller => controller.GetBehaviours<VRCAvatarParameterDriver>())
                .SelectMany(driver => driver.parameters)
                .Select(prm => prm.name)
                .ToHashSet();

            var floatsToOptimize = new HashSet<AnimatorControllerParameter>();
            void AttemptToAdd(string paramName) {
                if (string.IsNullOrEmpty(paramName)) return;
                
                var vrcParam = manager.GetParams().GetParam(paramName);
                if (vrcParam == null) return;
                var synced = (bool)networkSyncedField.GetValue(vrcParam);
                if (!synced) return;

                var animParam = GetFx().GetRaw().GetParam(paramName);
                if (animParam == null || animParam.type != AnimatorControllerParameterType.Float) return;
                
                if(drivenParams.Contains(paramName)) return;

                floatsToOptimize.Add(animParam);
            }

            manager.GetMenu().GetRaw().ForEachMenu(ForEachItem: (control, list) => {
                if (control.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet) {
                    AttemptToAdd(control.GetSubParameter(0)?.name);
                }

                return Continue;
            });
            
            return floatsToOptimize.Select(prm => new VFAFloat(prm)).ToList();
        }

        public override string GetEditorTitle() {
            return "Unlimited Params (BETA)";
        }

        public override bool OnlyOneAllowed() {
            return true;
        }

        public override bool AvailableOnRootOnly() {
            return true;
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This component will optimize all synced float parameters used in radial menu toggles into 16 total bits"));
            content.Add(VRCFuryEditorUtils.Warn(
                "This feature is in BETA - Please report any issues on the VRCFury discord"));
            return content;
        }
    }
}
