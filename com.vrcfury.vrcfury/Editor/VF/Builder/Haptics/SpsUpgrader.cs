using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Component;
using VF.Inspector;
using VF.Menu;
using VF.Model;
using VF.Model.StateAction;
using VF.Utils;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Builder.Haptics {
    internal static class SpsUpgrader {
        private const string dialogTitle = "VRCFury Legacy Haptics Upgrader";
        
        public static void Run() {
            var avatarObject = MenuUtils.GetSelectedAvatar();
            if (avatarObject == null) avatarObject = Selection.activeGameObject.asVf().root;

            var messages = Apply(avatarObject, true, Mode.Manual);
            if (string.IsNullOrWhiteSpace(messages)) {
                DialogUtils.DisplayDialog(
                    dialogTitle,
                    "VRCFury failed to find any parts to upgrade! Ask on the discord?",
                    "Ok"
                );
                return;
            }
        
            var doIt = DialogUtils.DisplayDialog(
                dialogTitle,
                messages + "\n\nContinue?",
                "Yes, Do it!",
                "Cancel"
            );
            if (!doIt) return;

            Apply(avatarObject, false, Mode.Manual);
            DialogUtils.DisplayDialog(
                dialogTitle,
                "Upgrade complete!",
                "Ok"
            );

            var sv = EditorWindowFinder.GetWindows<SceneView>().FirstOrDefault();
            if (sv != null) sv.drawGizmos = true;
        }

        public static bool Check() {
            if (Selection.activeGameObject == null) return false;
            return true;
        }

        private static bool IsHapticContact(UnityEngine.Component c, List<string> collisionTags) {
            var obj = c.owner();
            if (collisionTags.Any(t => t.StartsWith("TPSVF_"))) return true;
            else if (obj.name.StartsWith("OGB_")) return true;
            return false;
        }

        public enum Mode {
            Manual,
            AutomatedComponent,
            AutomatedForEveryone
        }
        public static string Apply(VFGameObject avatarObject, bool dryRun, Mode mode) {
            var objectsToDelete = new List<VFGameObject>();
            var componentsToDelete = new List<UnityEngine.Component>();
            var hasExistingSocket = new HashSet<VFGameObject>();
            var hasExistingPlug = new HashSet<VFGameObject>();
            var addedSocket = new HashSet<VFGameObject>();
            var addedSocketNames = new Dictionary<VFGameObject,string>();
            var addedPlug = new HashSet<VFGameObject>();
            var foundParentConstraint = false;

            bool AlreadyExistsAboveOrBelow(VFGameObject obj, IEnumerable<VFGameObject> enumerable) {
                var set = enumerable.ToImmutableHashSet();
                var parentIsDeleted = obj.GetSelfAndAllParents()
                    .Any(t => objectsToDelete.Contains(t));
                if (parentIsDeleted) return true;
                return obj.GetSelfAndAllChildren()
                    .Concat(obj.GetSelfAndAllParents())
                    .Any(set.Contains);
            }

            string GetPath(VFGameObject obj) {
                return obj.GetPath(avatarObject);
            }
            VRCFuryHapticPlug AddPlug(VFGameObject obj) {
                if (AlreadyExistsAboveOrBelow(obj, hasExistingPlug.Concat(addedPlug))) return null;
                addedPlug.Add(obj);
                if (dryRun) return null;
                var plug = obj.AddComponent<VRCFuryHapticPlug>();
                plug.enableSps = false;
                plug.sendersOnly = mode == Mode.AutomatedForEveryone;
                return plug;
            }
            VRCFuryHapticSocket AddSocket(VFGameObject obj) {
                if (AlreadyExistsAboveOrBelow(obj, hasExistingSocket.Concat(addedSocket))) return null;
                addedSocket.Add(obj);
                if (dryRun) return null;
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                socket.addLight = VRCFuryHapticSocket.AddLight.None;
                socket.addMenuItem = false;
                socket.sendersOnly = mode == Mode.AutomatedForEveryone;
                return socket;
            }

            foreach (var c in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>()) {
                hasExistingPlug.Add(c.owner());
                foreach (var renderer in VRCFuryHapticPlugEditor.GetRenderers(c)) {
                    hasExistingPlug.Add(renderer.owner());
                }
            }
            foreach (var c in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()) {
                hasExistingSocket.Add(c.owner());
            }
            
            // Upgrade "parent-constraint" DPS setups
            if (mode == Mode.Manual) {
                foreach (var parent in avatarObject.GetComponentsInSelfAndChildren<Transform>()) {
                    var constraint = parent.owner().GetConstraints().FirstOrDefault(c => c.IsParent());
                    if (constraint == null) continue;
                    var sourceTransforms = constraint.GetSources();
                    if (sourceTransforms.Length < 2) continue;
                    var sourcesWithWeight = 0;
                    foreach (var i in Enumerable.Range(0, sourceTransforms.Length)) {
                        if (constraint.GetWeight(i) > 0) sourcesWithWeight++;
                    }

                    if (sourcesWithWeight > 1) {
                        // This is probably not a parent constraint socket, but rather an actual position splitter.
                        // (used to position a socket between two bones)
                        continue;
                    }

                    var parentInfo = GetIsParent(parent);
                    if (parentInfo == null) continue;

                    var parentLightType = parentInfo.Item1;
                    var parentPosition = parentInfo.Item2;
                    var parentRotation = parentInfo.Item3;

                    foundParentConstraint = true;
                    objectsToDelete.Add(parent);

                    foreach (var i in Enumerable.Range(0, sourceTransforms.Length)) {
                        var t = sourceTransforms[i];
                        if (t == null) continue;
                        var sourcePositionOffset = constraint.GetPositionOffset(i);
                        var sourceRotationOffset = Quaternion.Euler(constraint.GetRotationOffset(i));
                        var name = HapticUtils.GetName(t);

                        var socket = AddSocket(t);
                        addedSocketNames[t] = name;
                        if (socket != null) {
                            socket.position = (sourcePositionOffset + sourceRotationOffset * parentPosition)
                                * constraint.owner().worldScale.x / socket.owner().worldScale.x;
                            socket.rotation = (sourceRotationOffset * parentRotation).eulerAngles;
                            socket.addLight = VRCFuryHapticSocket.AddLight.Auto;
                            socket.addMenuItem = true;
                            //t.name = "Haptic Socket";

                            if (name.ToLower().Contains("vag")) {
                                AddBlendshapeIfPresent(avatarObject, socket,
                                    VRCFuryEditorUtils.Rev("2ECIFIRO"), new Vector2(0, 0.03f));
                            }

                            if (VRCFuryHapticSocketEditor.ShouldProbablyHaveTouchZone(socket)) {
                                AddBlendshapeIfPresent(avatarObject, socket,
                                    VRCFuryEditorUtils.Rev("egluBymmuT"), new Vector2(-0.15f, 0));
                            }
                        }
                    }
                }
            }

            // Un-bake baked components
            foreach (var t in avatarObject.GetComponentsInSelfAndChildren<Transform>()) {
                if (!t) continue; // this can happen if we're visiting one of the things we deleted below

                void UnbakePen(Transform baked) {
                    if (!baked) return;
                    var info = baked.Find("Info");
                    if (!info) info = baked;
                    var p = AddPlug(baked.parent);
                    if (p) {
                        var size = info.Find("size");
                        if (size) {
                            p.length = size.localScale.x;
                            p.radius = size.localScale.y;
                        }
                        p.name = GetNameFromBakeInfo(info);
                    }
                    objectsToDelete.Add(baked);
                }
                void UnbakeOrf(Transform baked) {
                    if (!baked) return;
                    var info = baked.Find("Info");
                    if (!info) info = baked;
                    var o = AddSocket(baked.parent);
                    if (o) {
                        o.name = GetNameFromBakeInfo(info);
                    }
                    objectsToDelete.Add(baked);
                }

                UnbakePen(t.Find("OGB_Baked_Pen"));
                UnbakePen(t.Find("BakedOGBPenetrator"));
                UnbakePen(t.Find("BakedHapticPlug"));
                UnbakePen(t.Find("BakedSpsPlug"));
                UnbakeOrf(t.Find("OGB_Baked_Orf"));
                UnbakeOrf(t.Find("BakedOGBOrifice"));
                UnbakeOrf(t.Find("BakedHapticSocket"));
                UnbakeOrf(t.Find("BakedSpsSocket"));
            }
            
            // Auto-add plugs from DPS and TPS
            foreach (var renderer in avatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                if (TpsConfigurer.HasDpsOrTpsMaterial(renderer) && PlugSizeDetector.GetAutoWorldSize(renderer) != null)
                    AddPlug(renderer.owner());
            }
            
            // Auto-add sockets from DPS
            foreach (var light in avatarObject.GetComponentsInSelfAndChildren<Light>()) {
                var parent = light.owner().parent;
                if (parent) {
                    if (VRCFuryHapticSocketEditor.GetInfoFromLights(parent, true) != null)
                        AddSocket(parent);
                }
            }
            
            // Upgrade old OGB markers to components
            foreach (var t in avatarObject.GetSelfAndAllChildren()) {
                if (!t) continue; // this can happen if we're visiting one of the things we deleted below
                var penMarker = t.Find("OGB_Marker_Pen");
                if (penMarker) {
                    AddPlug(t);
                    objectsToDelete.Add(penMarker);
                }

                var holeMarker = t.Find("OGB_Marker_Hole");
                if (holeMarker) {
                    var o = AddSocket(t);
                    if (o) o.addLight = VRCFuryHapticSocket.AddLight.Hole;
                    objectsToDelete.Add(holeMarker);
                }
                
                var ringMarker = t.Find("OGB_Marker_Ring");
                if (ringMarker) {
                    var o = AddSocket(t);
                    if (o) o.addLight = VRCFuryHapticSocket.AddLight.Ring;
                    objectsToDelete.Add(ringMarker);
                }
            }
            
            // Claim lights on all OGB components
            if (mode == Mode.Manual || mode == Mode.AutomatedComponent) {
                foreach (var transform in hasExistingSocket.Concat(addedSocket)) {
                    if (!dryRun) {
                        foreach (var socket in transform.GetComponents<VRCFuryHapticSocket>()) {
                            if (socket.addLight == VRCFuryHapticSocket.AddLight.None) {
                                var info = VRCFuryHapticSocketEditor.GetInfoFromLights(socket.owner());
                                if (info != null) {
                                    var type = info.Item1;
                                    var position = info.Item2;
                                    var rotation = info.Item3;
                                    socket.addLight = type;
                                    socket.position = position;
                                    socket.rotation = rotation.eulerAngles;
                                }
                            }
                        }
                    }

                    VRCFuryHapticSocketEditor.ForEachPossibleLight(transform, false,
                        light => { componentsToDelete.Add(light); });
                }
            }

            // Clean up
            var deletions = new List<string>();
            if (mode == Mode.Manual) {
                deletions = AvatarCleaner.Cleanup(
                    avatarObject,
                    perform: !dryRun,
                    ShouldRemoveObj: obj => {
                        return obj.name == "GUIDES_DELETE"
                               || objectsToDelete.Contains(obj);
                    },
                    ShouldRemoveAsset: asset => {
                        if (asset == null) return false;
                        var path = AssetDatabase.GetAssetPath(asset);
                        if (path == null) return false;
                        var lower = path.ToLower();
                        if (lower.Contains("dps_attach")) return true;
                        return false;
                    },
                    ShouldRemoveLayer: layer => {
                        var lower = layer.ToLower();
                        if (foundParentConstraint && lower.Contains("tps") && lower.Contains("orifice")) {
                            return true;
                        }

                        if (foundParentConstraint && layer == "EZDPS Orifices") {
                            return true;
                        }

                        return layer == "DPS_Holes"
                               || layer == "DPS_Rings"
                               || layer == "HotDog"
                               || layer == "DPS Orifice"
                               || layer == "Orifice Position";
                    },
                    ShouldRemoveParam: param => {
                        return param == "DPS_Hole"
                               || param == "DPS_Ring"
                               || param == "HotDog"
                               || param == "fluff/dps/orifice"
                               || param == "EZDPS/Orifice"
                               || (param.StartsWith("TPS") && param.Contains("/VF"))
                               || param.StartsWith("OGB/")
                               || param.StartsWith("Nsfw/Ori/");
                    },
                    ShouldRemoveComponent: component => {
                        if (component is VRCContactSender sender && IsHapticContact(sender, sender.collisionTags))
                            return true;
                        if (component is VRCContactReceiver rcv && IsHapticContact(rcv, rcv.collisionTags)) return true;
                        if (componentsToDelete.Contains(component)) return true;
                        return false;
                    }
                );
            }

            var parts = new List<string>();
            var alreadyExists = hasExistingSocket
                .Concat(hasExistingPlug)
                .ToImmutableHashSet();
            if (addedPlug.Count > 0)
                parts.Add("Plug component will be added to:\n" + addedPlug.Select(GetPath).Join('\n'));

            string GetSocketLine(VFGameObject t) {
                if (addedSocketNames.TryGetValue(t, out var name)) {
                    return GetPath(t) + " (" + name + ")";
                }
                return GetPath(t);
            }
            if (addedSocket.Count > 0)
                parts.Add("Socket component will be added to:\n" + addedSocket.Select(GetSocketLine).Join('\n'));
            if (deletions.Count > 0)
                parts.Add("These objects will be deleted:\n" + deletions.Join('\n'));
            if (alreadyExists.Count > 0)
                parts.Add("SPS already exists on:\n" + alreadyExists.Select(GetPath).Join('\n'));

            if (parts.Count == 0) return "";
            return parts.Join("\n\n");
        }

        private static string GetNameFromBakeInfo(VFGameObject marker) {
            foreach (var child in marker.Children()) {
                var name = child.name;
                if (name.StartsWith("name=")) {
                    return name.Substring(5);
                }
            }
            return "";
        }

        private static Tuple<VRCFuryHapticSocket.AddLight, Vector3, Quaternion> GetIsParent(VFGameObject obj) {
            var lightInfo = VRCFuryHapticSocketEditor.GetInfoFromLights(obj, true);
            if (lightInfo == null) {
                var child = obj.Find("Orifice");
                if (child != null && obj.childCount == 1) {
                    lightInfo = VRCFuryHapticSocketEditor.GetInfoFromLights(child, true);
                }
            }
            if (lightInfo != null) {
                return lightInfo;
            }

            // For some reason, on some avatars, this one doesn't have child lights even though it's supposed to
            if (obj.name == "__dps_lightobject") {
                return Tuple.Create(VRCFuryHapticSocket.AddLight.Ring, Vector3.zero, Quaternion.Euler(90, 0, 0));
            }

            return null;
        }

        private static void AddBlendshapeIfPresent(
            VFGameObject avatarObject,
            VRCFuryHapticSocket socket,
            string name,
            Vector2 range
        ) {
            if (HasBlendshape(avatarObject, name)) {
                socket.depthActions2.Add(new VRCFuryHapticSocket.DepthActionNew() {
                    actionSet = new State() {
                        actions = {
                            new BlendShapeAction {
                                blendShape = name
                            }
                        }
                    },
                    range = range
                });
            }
        }
        private static bool HasBlendshape(VFGameObject avatarObject, string name) {
            return avatarObject.GetComponentsInSelfAndChildren<Renderer>()
                .Any(skin => skin.HasBlendshape(name));
        }
    }
}
