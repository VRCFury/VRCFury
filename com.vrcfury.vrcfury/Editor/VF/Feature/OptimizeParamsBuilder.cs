using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder.Exceptions;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Feature {
    public class OptimizeParamsBuilder : FeatureBuilder<OptimizeParams> {
        [VFAutowired] private readonly ExceptionService excService;

        private static readonly FieldInfo networkSyncedField =
            typeof(VRCExpressionParameters.Parameter).GetField("networkSynced");

        [FeatureBuilderAction(FeatureOrder.OptimizeParams)]
        public void Apply() {
            if (networkSyncedField == null) {
                // can't optimize
                Debug.Log("Network Sync Field not available, Param Optimizer unable to run. Please update VRCSDK");
                return;
            }
            var p = manager.GetParams();
            var maxBits = VRCExpressionParameters.MAX_PARAMETER_COST;
            if (maxBits > 9999) {
                // Some versions of the VRChat SDK have a broken value for this
                maxBits = 256;
            }
            var totalCost = p.GetRaw().CalcTotalCost();
            if (totalCost <= maxBits) {
                // nothing to optimize
                return;
            }

            List<string> bools = new List<string>();
            List<string> floats = new List<string>();
            List<string> ints = new List<string>();

            foreach (var param in p.GetRaw().parameters)
            {
                if (!param.networkSynced) continue;

                switch(param.valueType) {
                    case VRCExpressionParameters.ValueType.Bool:
                        bools.Add(param.name);
                        break;
                    case VRCExpressionParameters.ValueType.Float:
                        floats.Add(param.name);
                        break;
                    case VRCExpressionParameters.ValueType.Int:
                        ints.Add(param.name);
                        break;
                }
            }

            if (totalCost + 16 - bools.Count() <= maxBits) {
                ints.Clear();
                floats.Clear();
                Debug.Log("Optimizing Bools");
            } else if (totalCost + 24 - ints.Count()*8 - bools.Count()*8 <= maxBits) {
                floats.Clear();
                Debug.Log("Optimizing Bools and Ints");
            } else {
                Debug.Log("Optimizing Bools, Floats, and Ints");
            }

            var minValue = Math.Max(1, new [] {bools.Count(), ints.Count(), floats.Count() }.Min());

            var boolsPerSet = bools.Count() / minValue;
            var intsPerSet = ints.Count() / minValue;
            var floatsPerSet = floats.Count() / minValue;

            var paramsPerSet = 0;
            var bitsPerSet = 0;
            var minSlotsNeeded = 0;

            if (bools.Count() > 0) {
                paramsPerSet += boolsPerSet;
                bitsPerSet += boolsPerSet;
                minSlotsNeeded++;
            }
            if (ints.Count() > 0) {
                paramsPerSet += intsPerSet;
                bitsPerSet += 8 * intsPerSet;
                minSlotsNeeded++;
            }
            if (floats.Count() > 0) {
                paramsPerSet += floatsPerSet;
                bitsPerSet += 8 * floatsPerSet;
                minSlotsNeeded++;
            }

            totalCost -= bools.Count();
            totalCost -= ints.Count()*8;
            totalCost -= floats.Count()*8;

            var paramSlotsAvailable = 256 - p.GetRaw().parameters.Length;

            paramSlotsAvailable--; // index int
            totalCost+=8; // index int

            var setCount = 0;

            if (paramSlotsAvailable >= paramsPerSet) {
                setCount = Math.Min(paramSlotsAvailable / paramsPerSet, (maxBits - totalCost) / bitsPerSet);
            } else if (paramSlotsAvailable < minSlotsNeeded) {
                 excService.ThrowIfActuallyUploading(new SneakyException(
                    $"Your avatar is using too many synced and unsynced expression parameters and they can't be further optimized!"
                    + " A bug in vrchat causes this to unexpectedly throw away some of your parameters.\n\n" +
                    "https://feedback.vrchat.com/avatar-30/p/1332-bug-vrcexpressionparameters-fail-to-load-correctly-with-more-than-256-param"));
            } else {
                setCount = 1;
                intsPerSet = 1;
                floatsPerSet = 1;
                boolsPerSet = paramSlotsAvailable - 2;
            }


            Dictionary<string, (int, int)> paramMap = new Dictionary<string, (int, int)>();

            var index = 0;
            foreach (var param in bools) {
                paramMap[param] = (index / (boolsPerSet * setCount), index % (boolsPerSet * setCount));
                index++;
            }

            index = 0;
            foreach(var param in floats) {
                paramMap[param] = (index / (floatsPerSet * setCount), index % (floatsPerSet * setCount));
                index++;
            }

            index = 0;
            foreach(var param in ints) {
                paramMap[param] = (index / (intsPerSet * setCount), index % (intsPerSet * setCount));
                index++;
            }

            var syncIndex = fx.NewInt("SYNC", synced: true, def: -1);
            List<VFAParam> syncBools = new List<VFAParam>();
            List<VFAParam> syncInts = new List<VFAParam>();
            List<VFAParam> syncFloats = new List<VFAParam>();

            for (var i = 0; i < setCount; i++) {
                if (bools.Count() > 0) {
                    for (var j = 0; j < boolsPerSet; j++) {
                        syncBools.Add(fx.NewBool("SYNC_BOOL_" + (i * boolsPerSet + j), synced: true));
                    }
                }

                if (floats.Count() > 0) {
                    for (var j = 0; j < floatsPerSet; j++) {
                        syncFloats.Add(fx.NewFloat("SYNC_FLOAT_" + (i * floatsPerSet + j), synced: true));
                    }
                }

                if (ints.Count() > 0) {
                    for (var j = 0; j < intsPerSet; j++) {
                        syncInts.Add(fx.NewInt("SYNC_INT_" + (i * intsPerSet + j), synced: true));
                    }
                }
            }

            var maxIndex = new [] { bools.Count() / (boolsPerSet * setCount), ints.Count() / (intsPerSet * setCount), floats.Count() / (floatsPerSet * setCount) }.Max() + 1;

            var localLayer = fx.NewLayer("Optimized Sync Local");
            var remoteLayer = fx.NewLayer("Optimized Sync Remote");

            List<VFState> localStates = new List<VFState>();
            List<VFState> remoteStates = new List<VFState>();

            var localStart = localLayer.NewState("Start");
            remoteLayer.NewState("Start");

            VFState lastLocal = null;

            for (var i = 0; i < maxIndex; i++) {
                var localState = localLayer.NewState("Sync " + i).Drives(syncIndex.Name(), i);
                if (lastLocal != null) {
                    lastLocal.TransitionsTo(localState).When(fx.True().IsTrue());
                }
                lastLocal = localState;
                localStates.Add(localState);

                var remoteState = remoteLayer.NewState("Sync " + i);
                remoteState.TransitionsFromAny().When(syncIndex.IsEqualTo(i).And(fx.IsLocal().IsFalse()));
                remoteStates.Add(remoteState);
            }

            localStart.TransitionsTo(localStates[0]).When(fx.IsLocal().IsTrue());
            lastLocal.TransitionsTo(localStates[0]).When(fx.True().IsTrue());
            
            foreach (var param in bools) {
                fx.UnsyncParam(param);
                var (indexVal, offsetVal) = paramMap[param];
                localStates[indexVal].DrivesCopy(syncBools[offsetVal].Name(), param, false);
                remoteStates[indexVal].DrivesCopy(param, syncBools[offsetVal].Name(), false);
            }

            foreach (var param in ints) {
                fx.UnsyncParam(param);
                var (indexVal, offsetVal) = paramMap[param];
                localStates[indexVal].DrivesCopy(syncInts[offsetVal].Name(), param, false);
                remoteStates[indexVal].DrivesCopy(param, syncInts[offsetVal].Name(), false);
            }

            foreach (var param in floats) {
                fx.UnsyncParam(param);
                var (indexVal, offsetVal) = paramMap[param];
                localStates[indexVal].DrivesCopy(syncFloats[offsetVal].Name(), param, false);
                remoteStates[indexVal].DrivesCopy(param, syncFloats[offsetVal].Name(), false);
            }
        }

        public override string GetEditorTitle() {
            return "Parameter Optimizer";
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
                "This feature will attempt to reduce the amount of synced params used by your avatar by syncing them in a round " +
                "robin method. It will attempt to optimize bools, then ints, then floats in that order. This may cause parameters " +
                "to sync slightly slower in some cases, but allows for more params than would usually be allowed on an avatar. Due " +
                "to some of VRC's limitations, it still may not be possible to optimize the avatar in certain configurations."));
            return content;
        }
    }
}
