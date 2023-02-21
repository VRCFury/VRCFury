using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Inspector;
using VF.Model;
using VF.Model.StateAction;
using VRC.SDK3.Dynamics.Contact.Components;
using Component = UnityEngine.Component;

namespace VF.Menu {
    public class OGBUpgradeMenuItem {
        private static string dialogTitle = "OGB Upgrader";
        
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

        private static bool IsOGBContact(Component c, List<string> collisionTags) {
            if (collisionTags.Any(t => t.StartsWith("TPSVF_"))) return true;
            else if (c.gameObject.name.StartsWith("OGB_")) return true;
            return false;
        }

        public static string Apply(GameObject avatarObject, bool dryRun) {
            var deletions = new List<string>();
            var addedPen = new HashSet<GameObject>();
            var addedOrf = new HashSet<GameObject>();
            var alreadyExists = new List<string>();

            string GetPath(GameObject obj) {
                return AnimationUtility.CalculateTransformPath(obj.transform, avatarObject.transform);
            }
            OGBPenetrator AddPen(GameObject obj) {
                if (obj.GetComponentsInParent<OGBPenetrator>(true).Length > 0) return null;
                if (obj.GetComponentsInChildren<OGBPenetrator>(true).Length > 0) return null;
                if (addedPen.Contains(obj)) return null;
                addedPen.Add(obj);
                if (dryRun) return null;
                return obj.AddComponent<OGBPenetrator>();
            }
            OGBOrifice AddOrifice(GameObject obj) {
                if (obj.GetComponentsInParent<OGBOrifice>(true).Length > 0) return null;
                if (obj.GetComponentsInChildren<OGBOrifice>(true).Length > 0) return null;
                if (addedOrf.Contains(obj)) return null;
                addedOrf.Add(obj);
                if (dryRun) return null;
                return obj.AddComponent<OGBOrifice>();
            }
            void Delete(GameObject obj) {
                if (dryRun) {
                    deletions.Add(GetPath(obj));
                    return;
                }
                AvatarCleaner.RemoveObject(obj);
            }

            foreach (var c in avatarObject.GetComponentsInChildren<OGBPenetrator>(true)) {
                alreadyExists.Add(GetPath(c.gameObject));
            }
            foreach (var c in avatarObject.GetComponentsInChildren<OGBOrifice>(true)) {
                alreadyExists.Add(GetPath(c.gameObject));
            }
            
            // Upgrade "parent-constraint" DPS setups
            var oldParentsToDelete = new HashSet<GameObject>();
            foreach (var parent in avatarObject.GetComponentsInChildren<Transform>(true)) {
                var constraint = parent.gameObject.GetComponent<ParentConstraint>();
                if (constraint == null) continue;
                if (constraint.sourceCount < 2) continue;
                var sourcesWithWeight = 0;
                for (var i = 0; i < constraint.sourceCount; i++) {
                    if (constraint.GetSource(i).weight > 0) sourcesWithWeight++;
                }
                if (sourcesWithWeight > 1) {
                    // This is probably not a parent constraint orifice, but rather an actual position splitter.
                    // (used to position an orifice between two bones)
                    continue;
                }
                
                var parentInfo = GetIsParent(parent.gameObject);
                if (parentInfo == null) continue;

                var parentLightType = parentInfo.Item1;
                var parentPosition = parentInfo.Item2;
                var parentRotation = parentInfo.Item3;

                oldParentsToDelete.Add(parent.gameObject);
                
                for (var i = 0; i < constraint.sourceCount; i++) {
                    var source = constraint.GetSource(i);
                    var sourcePositionOffset = constraint.GetTranslationOffset(i);
                    var sourceRotationOffset = Quaternion.Euler(constraint.GetRotationOffset(i));
                    var t = source.sourceTransform;
                    if (t == null) continue;
                    var obj = t.gameObject;
                    var name = obj.name;
                    var id = name.IndexOf("(");
                    if (id >= 0) name = name.Substring(id+1);
                    id = name.IndexOf(")");
                    if (id >= 0) name = name.Substring(0, id);
                    // Convert camel case to spaces
                    name = Regex.Replace(name, "(\\B[A-Z])", " $1");
                    name = name.ToLower();
                    name = name.Replace("dps", "");
                    name = name.Replace("orifice", "");
                    name = name.Replace('_', ' ');
                    name = name.Replace('-', ' ');
                    while (name.Contains("  ")) {
                        name = name.Replace("  ", " ");
                    }
                    name = name.Trim();
                    name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);

                    var fullName = "Orifice (" + name + ")";

                    addedOrf.Add(obj);
                    if (!dryRun) {
                        var ogb = obj.GetComponent<OGBOrifice>();
                        if (ogb == null) ogb = obj.AddComponent<OGBOrifice>();
                        ogb.position = (sourcePositionOffset + sourceRotationOffset * parentPosition)
                            * constraint.transform.lossyScale.x / ogb.transform.lossyScale.x;
                        ogb.rotation = (sourceRotationOffset * parentRotation).eulerAngles;
                        ogb.addLight = OGBOrifice.AddLight.Auto;
                        ogb.name = name;
                        ogb.addMenuItem = true;
                        obj.name = fullName;
                        
                        if (name.ToLower().Contains("vag")) {
                            AddBlendshapeIfPresent(avatarObject, ogb, "ORIFICE2", -0.03f, 0);
                        }
                        if (OGBOrificeEditor.ShouldProbablyHaveTouchZone(ogb)) {
                            AddBlendshapeIfPresent(avatarObject, ogb, "TummyBulge", 0, 0.15f);
                        }
                    }
                }
            }
            
            // Un-bake baked components
            foreach (var t in avatarObject.GetComponentsInChildren<Transform>(true)) {
                if (!t) continue; // this can happen if we're visiting one of the things we deleted below

                void UnbakePen(Transform baked) {
                    if (!baked) return;
                    var info = baked.Find("Info");
                    if (!info) info = baked;
                    var p = AddPen(baked.parent.gameObject);
                    if (p) {
                        var size = info.Find("size");
                        if (size) {
                            p.length = size.localScale.x;
                            p.radius = size.localScale.y;
                        }
                        p.name = GetNameFromBakeInfo(info.gameObject);
                    }
                    Delete(baked.gameObject);
                }
                void UnbakeOrf(Transform baked) {
                    if (!baked) return;
                    var info = baked.Find("Info");
                    if (!info) info = baked;
                    var o = AddOrifice(baked.parent.gameObject);
                    if (o) {
                        o.name = GetNameFromBakeInfo(info.gameObject);
                        OGBOrificeEditor.ClaimLights(o);
                    }
                    Delete(baked.gameObject);
                }

                UnbakePen(t.Find("OGB_Baked_Pen"));
                UnbakePen(t.Find("BakedOGBPenetrator"));
                UnbakeOrf(t.Find("OGB_Baked_Orf"));
                UnbakeOrf(t.Find("BakedOGBOrifice"));
            }
            
            // Auto-add DPS and TPS penetrators
            foreach (var skin in avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                if (OGBPenetratorSizeDetector.GetAutoWorldSize(skin.gameObject, true) != null)
                    AddPen(skin.gameObject);
            }
            foreach (var mesh in avatarObject.GetComponentsInChildren<MeshRenderer>(true)) {
                if (OGBPenetratorSizeDetector.GetAutoWorldSize(mesh.gameObject, true) != null)
                    AddPen(mesh.gameObject);
            }
            
            // Auto-add DPS orifices
            foreach (var light in avatarObject.GetComponentsInChildren<Light>(true)) {
                var parent = light.gameObject.transform.parent;
                if (parent) {
                    var parentObj = parent.gameObject;
                    if (!oldParentsToDelete.Contains(parentObj) && OGBOrificeEditor.GetInfoFromLights(parentObj, true) != null)
                        AddOrifice(parentObj);
                }
            }
            
            // Upgrade old OGB markers to components
            foreach (var t in avatarObject.GetComponentsInChildren<Transform>(true)) {
                if (!t) continue; // this can happen if we're visiting one of the things we deleted below
                var penMarker = t.Find("OGB_Marker_Pen");
                if (penMarker) {
                    AddPen(t.gameObject);
                    Delete(penMarker.gameObject);
                }

                var holeMarker = t.Find("OGB_Marker_Hole");
                if (holeMarker) {
                    var o = AddOrifice(t.gameObject);
                    if (o) o.addLight = OGBOrifice.AddLight.Hole;
                    Delete(holeMarker.gameObject);
                }
                
                var ringMarker = t.Find("OGB_Marker_Ring");
                if (ringMarker) {
                    var o = AddOrifice(t.gameObject);
                    if (o) o.addLight = OGBOrifice.AddLight.Ring;
                    Delete(ringMarker.gameObject);
                }
            }
            
            // Claim lights on all OGB components
            foreach (var orifice in avatarObject.GetComponentsInChildren<OGBOrifice>(true)) {
                if (dryRun) {
                    foreach (var light in orifice.gameObject.GetComponentsInChildren<Light>(true)) {
                        deletions.Add("Light on " + GetPath(light.gameObject));
                    }
                } else {
                    OGBOrificeEditor.ClaimLights(orifice);
                }
            }

            // Clean up
            deletions.AddRange(AvatarCleaner.Cleanup(
                avatarObject,
                perform: !dryRun,
                ShouldRemoveObj: obj => {
                    return obj.name == "GUIDES_DELETE"
                           || oldParentsToDelete.Contains(obj);
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
                    if (oldParentsToDelete.Count > 0 && lower.Contains("tps") && lower.Contains("orifice")) {
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
                           || (param.StartsWith("TPS") && param.Contains("/VF"))
                           || param.StartsWith("OGB/")
                           || param.StartsWith("Nsfw/Ori/");
                },
                ShouldRemoveComponent: component => {
                    if (component is VRCContactSender sender && IsOGBContact(sender, sender.collisionTags)) return true;
                    if (component is VRCContactReceiver rcv && IsOGBContact(rcv, rcv.collisionTags)) return true;
                    return false;
                }
            ));

            var parts = new List<string>();
            if (addedPen.Count > 0)
                parts.Add("OGB Penetrator component will be added to:\n" + string.Join("\n", addedPen.Select(GetPath)));
            if (addedOrf.Count > 0)
                parts.Add("OGB Orifice component will be added to:\n" + string.Join("\n", addedOrf.Select(GetPath)));
            if (deletions.Count > 0)
                parts.Add("These objects will be deleted:\n" + string.Join("\n", deletions));
            if (alreadyExists.Count > 0)
                parts.Add("OGB already exists on:\n" + string.Join("\n", alreadyExists));

            if (parts.Count == 0) return "";
            return string.Join("\n\n", parts);
        }

        private static string GetNameFromBakeInfo(GameObject marker) {
            foreach (Transform child in marker.transform) {
                if (child.name.StartsWith("name=")) {
                    return child.name.Substring(5);
                }
            }
            return "";
        }

        private static Tuple<OGBOrifice.AddLight, Vector3, Quaternion> GetIsParent(GameObject obj) {
            var lightInfo = OGBOrificeEditor.GetInfoFromLights(obj, true);
            if (lightInfo != null) {
                return lightInfo;
            }

            // For some reason, on some avatars, this one doesn't have child lights even though it's supposed to
            if (obj.name == "__dps_lightobject") {
                return Tuple.Create(OGBOrifice.AddLight.Ring, Vector3.zero, Quaternion.Euler(90, 0, 0));
            }

            return null;
        }

        private static void AddBlendshapeIfPresent(
            GameObject avatarObject,
            OGBOrifice orf,
            string name,
            float minDepth,
            float maxDepth
        ) {
            if (HasBlendshape(avatarObject, name)) {
                orf.depthActions.Add(new OGBOrifice.DepthAction() {
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
        private static bool HasBlendshape(GameObject avatarObject, string name) {
            var skins = avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
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
