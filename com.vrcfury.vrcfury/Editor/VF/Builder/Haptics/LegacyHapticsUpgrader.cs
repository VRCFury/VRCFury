using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Component;
using VF.Inspector;
using VF.Menu;
using VF.Model;
using VF.Model.StateAction;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Builder.Haptics {
    public static class LegacyHapticsUpgrader {
        private static string dialogTitle = "VRCFury Legacy Haptics Upgrader";
        
        public static void Run() {
            var avatarObject = MenuUtils.GetSelectedAvatar();
            if (avatarObject == null) { 
                avatarObject = Selection.activeGameObject;
                while (avatarObject.transform.parent != null) avatarObject = avatarObject.transform.parent.gameObject;
            }

            var messages = Apply(avatarObject, true);
            if (string.IsNullOrWhiteSpace(messages)) {
                EditorUtility.DisplayDialog(
                    dialogTitle,
                    "VRCFury failed to find any parts to upgrade! Ask on the discord?",
                    "Ok"
                );
                return;
            }
        
            var doIt = EditorUtility.DisplayDialog(
                dialogTitle,
                messages + "\n\nContinue?",
                "Yes, Do it!",
                "Cancel"
            );
            if (!doIt) return;

            Apply(avatarObject, false);
            EditorUtility.DisplayDialog(
                dialogTitle,
                "Upgrade complete!",
                "Ok"
            );

            SceneView sv = EditorWindow.GetWindow<SceneView>();
            if (sv != null) sv.drawGizmos = true;
        }

        public static bool Check() {
            if (Selection.activeGameObject == null) return false;
            return true;
        }

        private static bool IsHapticContact(UnityEngine.Component c, List<string> collisionTags) {
            VFGameObject obj = c.gameObject;
            if (collisionTags.Any(t => t.StartsWith("TPSVF_"))) return true;
            else if (obj.name.StartsWith("OGB_")) return true;
            return false;
        }

        public static string Apply(VFGameObject avatarObject, bool dryRun) {
            var objectsToDelete = new List<VFGameObject>();
            var componentsToDelete = new List<UnityEngine.Component>();
            var hasExistingSocket = new HashSet<VFGameObject>();
            var hasExistingPlug = new HashSet<VFGameObject>();
            var addedSocket = new HashSet<VFGameObject>();
            var addedSocketNames = new Dictionary<VFGameObject,string>();
            var addedPlug = new HashSet<VFGameObject>();
            var foundParentConstraint = false;

            bool AlreadyExistsAboveOrBelow(VFGameObject obj, IEnumerable<VFGameObject> list) {
                var parentIsDeleted = obj.GetSelfAndAllParents()
                    .Any(t => objectsToDelete.Contains(t));
                if (parentIsDeleted) return true;
                return obj.GetSelfAndAllChildren()
                    .Concat(obj.GetSelfAndAllParents())
                    .Any(list.Contains);
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
                return plug;
            }
            VRCFuryHapticSocket AddSocket(VFGameObject obj) {
                if (AlreadyExistsAboveOrBelow(obj, hasExistingSocket.Concat(addedSocket))) return null;
                addedSocket.Add(obj);
                if (dryRun) return null;
                var socket = obj.AddComponent<VRCFuryHapticSocket>();
                socket.addLight = VRCFuryHapticSocket.AddLight.None;
                socket.addMenuItem = false;
                return socket;
            }

            foreach (var c in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticPlug>()) {
                hasExistingPlug.Add(c.transform);
                foreach (var renderer in VRCFuryHapticPlugEditor.GetRenderers(c)) {
                    hasExistingPlug.Add(renderer.transform);
                }
            }
            foreach (var c in avatarObject.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>()) {
                hasExistingSocket.Add(c.transform);
            }
            
            // Upgrade "parent-constraint" DPS setups
            foreach (var parent in avatarObject.GetComponentsInSelfAndChildren<Transform>()) {
                var constraint = parent.gameObject.GetComponent<ParentConstraint>();
                if (constraint == null) continue;
                if (constraint.sourceCount < 2) continue;
                var sourcesWithWeight = 0;
                for (var i = 0; i < constraint.sourceCount; i++) {
                    if (constraint.GetSource(i).weight > 0) sourcesWithWeight++;
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
                
                for (var i = 0; i < constraint.sourceCount; i++) {
                    var source = constraint.GetSource(i);
                    var sourcePositionOffset = constraint.GetTranslationOffset(i);
                    var sourceRotationOffset = Quaternion.Euler(constraint.GetRotationOffset(i));
                    VFGameObject t = source.sourceTransform;
                    if (t == null) continue;
                    var name = HapticUtils.GetName(t);

                    var socket = AddSocket(t);
                    addedSocketNames[t] = name;
                    if (socket != null) {
                        socket.position = (sourcePositionOffset + sourceRotationOffset * parentPosition)
                            * constraint.transform.lossyScale.x / socket.transform.lossyScale.x;
                        socket.rotation = (sourceRotationOffset * parentRotation).eulerAngles;
                        socket.addLight = VRCFuryHapticSocket.AddLight.Auto;
                        socket.addMenuItem = true;
                        //t.name = "Haptic Socket";
                        
                        if (name.ToLower().Contains("vag")) {
                            AddBlendshapeIfPresent(avatarObject.transform, socket, VRCFuryEditorUtils.Rev("2ECIFIRO"), -0.03f, 0);
                        }
                        if (VRCFuryHapticSocketEditor.ShouldProbablyHaveTouchZone(socket)) {
                            AddBlendshapeIfPresent(avatarObject.transform, socket, VRCFuryEditorUtils.Rev("egluBymmuT"), 0, 0.15f);
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
                UnbakeOrf(t.Find("OGB_Baked_Orf"));
                UnbakeOrf(t.Find("BakedOGBOrifice"));
                UnbakePen(t.Find("BakedHapticSocket"));
            }
            
            // Auto-add plugs from DPS and TPS
            foreach (var tuple in RendererIterator.GetRenderersWithMeshes(avatarObject)) {
                var (renderer, _, _) = tuple;
                if (TpsConfigurer.HasDpsOrTpsMaterial(renderer) && PlugSizeDetector.GetAutoWorldSize(renderer) != null)
                    AddPlug(renderer.transform);
            }
            
            // Auto-add sockets from DPS
            foreach (var light in avatarObject.GetComponentsInSelfAndChildren<Light>()) {
                var parent = light.transform.parent;
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
            foreach (var transform in hasExistingSocket.Concat(addedSocket)) {
                if (!dryRun) {
                    foreach (var socket in transform.GetComponents<VRCFuryHapticSocket>()) {
                        if (socket.addLight == VRCFuryHapticSocket.AddLight.None) {
                            var info = VRCFuryHapticSocketEditor.GetInfoFromLights(socket.transform);
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

                VRCFuryHapticSocketEditor.ForEachPossibleLight(transform, false, light => {
                    componentsToDelete.Add(light);
                });
            }

            // Clean up
            var deletions = AvatarCleaner.Cleanup(
                avatarObject,
                perform: !dryRun,
                ShouldRemoveObj: obj => {
                    return obj.name == "GUIDES_DELETE" 
                           || objectsToDelete.Contains(obj.transform);
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
                    if (component is VRCContactSender sender && IsHapticContact(sender, sender.collisionTags)) return true;
                    if (component is VRCContactReceiver rcv && IsHapticContact(rcv, rcv.collisionTags)) return true;
                    if (componentsToDelete.Contains(component)) return true;
                    return false;
                }
            );

            var parts = new List<string>();
            var alreadyExists = hasExistingSocket
                .Concat(hasExistingPlug)
                .ToImmutableHashSet();
            if (addedPlug.Count > 0)
                parts.Add("Plug component will be added to:\n" + string.Join("\n", addedPlug.Select(GetPath)));

            string GetSocketLine(VFGameObject t) {
                if (addedSocketNames.ContainsKey(t)) {
                    return GetPath(t) + " (" + addedSocketNames[t] + ")";
                }
                return GetPath(t);
            }
            if (addedSocket.Count > 0)
                parts.Add("Socket component will be added to:\n" + string.Join("\n", addedSocket.Select(GetSocketLine)));
            if (deletions.Count > 0)
                parts.Add("These objects will be deleted:\n" + string.Join("\n", deletions));
            if (alreadyExists.Count > 0)
                parts.Add("Haptics already exists on:\n" + string.Join("\n", alreadyExists.Select(GetPath)));

            if (parts.Count == 0) return "";
            return string.Join("\n\n", parts);
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
            Transform avatarObject,
            VRCFuryHapticSocket socket,
            string name,
            float minDepth,
            float maxDepth
        ) {
            if (HasBlendshape(avatarObject, name)) {
                socket.depthActions.Add(new VRCFuryHapticSocket.DepthAction() {
                    state = new State() {
                        actions = {
                            new BlendShapeAction {
                                blendShape = name
                            }
                        }
                    },
                    minDepth = minDepth,
                    maxDepth = maxDepth
                });
            }
        }
        private static bool HasBlendshape(VFGameObject avatarObject, string name) {
            var skins = avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>();
            foreach (var skin in skins) {
                if (!skin.sharedMesh) continue;
                var blendShapeIndex = skin.sharedMesh.GetBlendShapeIndex(name);
                if (blendShapeIndex < 0) continue;
                return true;
            }
            return false;
        }
    }
}
