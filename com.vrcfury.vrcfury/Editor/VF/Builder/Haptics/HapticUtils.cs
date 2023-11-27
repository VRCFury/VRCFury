using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VF.Component;
using VF.Inspector;
using VF.Menu;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using Random = System.Random;

namespace VF.Builder.Haptics {
    public class HapticUtils {
        public static string CONTACT_PEN_MAIN = "TPS_Pen_Penetrating";
        public static string CONTACT_PEN_WIDTH = "TPS_Pen_Width";
        public static string CONTACT_PEN_CLOSE = "TPS_Pen_Close";
        public static string CONTACT_PEN_ROOT = "TPS_Pen_Root";
        public static string TagTpsOrfRoot = "TPS_Orf_Root";
        public static string TagTpsOrfFront = "TPS_Orf_Norm";

        public static string TagSpsSocketRoot = "SPSLL_Socket_Root";
        public static string TagSpsSocketFront = "SPSLL_Socket_Front";
        public static string TagSpsSocketIsRing = "SPSLL_Socket_Ring";
        public static string TagSpsSocketIsHole = "SPSLL_Socket_Hole";

        public static readonly string[] SelfContacts = {
            "Hand",
            "Finger",
            "Foot"
        };
        public static readonly string[] BodyContacts = {
            "Head",
            "Hand",
            "Foot",
            "Finger"
        };
        
        private static readonly Random rand = new Random();

        public static string RandomTag() {
            return "TPSVF_" + rand.Next(100_000_000, 999_999_999);
        }

        public static void AddSender(
            Transform obj,
            Vector3 pos,
            String objName,
            float radius,
            string[] tags,
            float height = 0,
            Quaternion rotation = default,
            bool worldScale = true,
            bool useHipAvoidance = true
        ) {
            var isOnHips = IsDirectChildOfHips(obj) && useHipAvoidance;
            var suffixes = new List<string>();
            suffixes.Add("");
            if (!isOnHips) {
                suffixes.Add("_SelfNotOnHips");
            }
            tags = tags.SelectMany(tag => suffixes.Select(suffix => tag + suffix)).ToArray();

            var child = GameObjects.Create(objName, obj);
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) return;
            var sender = child.AddComponent<VRCContactSender>();
            sender.position = pos;
            sender.radius = radius;
            sender.collisionTags = new List<string>(tags);
            if (height > 0) {
                sender.shapeType = ContactBase.ShapeType.Capsule;
                sender.height = height;
                sender.rotation = rotation;
            }
            if (worldScale) {
                sender.position /= child.worldScale.x;
                sender.radius /= child.worldScale.x;
                sender.height /= child.worldScale.x;
            }
        }

        public enum ReceiverParty {
            Self,
            Others,
            Both
        }

        public static void RemoveTPSSenders(Transform obj) {
            var remove = new List<UnityEngine.Component>();
            foreach (Transform child in obj) {
                foreach (var sender in child.gameObject.GetComponents<VRCContactSender>()) {
                    if (IsTPSSender(sender)) {
                        Debug.Log("Deleting TPS sender on " + sender.gameObject);
                        remove.Add(sender);
                    }
                }
            }

            foreach (var c in remove) {
                AvatarCleaner.RemoveComponent(c);
            }

            if (obj.parent) {
                RemoveTPSSenders(obj.parent);
            }
        }

        public static bool IsTPSSender(VRCContactSender c) {
            if (c.collisionTags.Any(t => t == CONTACT_PEN_MAIN)) return true;
            if (c.collisionTags.Any(t => t == CONTACT_PEN_WIDTH)) return true;
            if (c.collisionTags.Any(t => t == TagTpsOrfRoot)) return true;
            if (c.collisionTags.Any(t => t == TagTpsOrfFront)) return true;
            return false;
        }

        public static string GetNextName(List<string> usedNames, string prefix) {
            for (int i = 0; ; i++) {
                var next = prefix + (i == 0 ? "" : i+"");
                if (!usedNames.Contains(next)) {
                    usedNames.Add(next);
                    return next;
                }
            }
        }

        private static bool IsZeroScale(Transform obj) {
            var scale = obj.localScale;
            return scale.x == 0 || scale.y == 0 || scale.z == 0;
        }
        private static bool IsNegativeScale(Transform obj) {
            var scale = obj.localScale;
            return scale.x < 0 || scale.y < 0 || scale.z < 0;
        }
        private static bool IsNonUniformScale(Transform obj) {
            var scale = obj.localScale;
            return Math.Abs(scale.x - scale.y) / scale.x > 0.05
                   || Math.Abs(scale.x - scale.z) / scale.x > 0.05;
        }
        public static void AssertValidScale(VFGameObject obj, string type) {
            var current = obj;
            while (true) {
                if (IsZeroScale(current)) {
                    throw new Exception(
                        "A haptic component exists on an object with zero scale." +
                        " This object must not be zero scale or size calculation will fail.\n\n" +
                        "Component path: " + obj.GetPath() + "\n" +
                        "Offending object: " + current.GetPath());
                }
                if (IsNegativeScale(current)) {
                    throw new Exception(
                        "A haptic component exists on an object with negative scale." +
                        " This object must have a positive scale or size calculation will fail.\n\n" +
                        "Component path: " + obj.GetPath() + "\n" +
                        "Offending object: " + current.GetPath());
                }
                if (IsNonUniformScale(current)) {
                    var bypass = obj.Find("ItsOkayThatOgbMightBeBroken") != null;
                    if (!bypass) {
                        throw new Exception(
                            "A haptic component exists on an object with a non-uniform scale." +
                            " This object (and all parents) must have an X, Y, and Z scale value that match" +
                            " each other, or size calculation will fail.\n\n" +
                            "Component path: " + obj.GetPath() + "\n" +
                            "Offending object: " + current.GetPath());
                    }
                }

                var parent = current.parent;
                if (parent == null) break;
                current = parent;
            }
        }

        public static Transform GetMeshRoot(Renderer r) {
            if (r is SkinnedMeshRenderer skin && skin.rootBone) {
                return skin.rootBone;
            }
            return r.transform;
        }
        
        public static string GetName(VFGameObject obj) {
            var current = obj;
            while (current != null) {
                var name = NormalizeName(current.name);
                if (!string.IsNullOrWhiteSpace(name)) {
                    return name;
                }
                current = current.parent;
            }
            return "Unknown";
        }

        private static string NormalizeName(string name) {
            name = Regex.Replace(name, @"ezdps_([a-z][a-z]?_?)?", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"dps", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"gameobject", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"object", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"armature", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"tps", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"haptic", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"socket", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"plug", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"\(\d+\)", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, VRCFuryEditorUtils.Rev("ecifiro"), "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, VRCFuryEditorUtils.Rev("ecafiro"), "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, VRCFuryEditorUtils.Rev("rotartenep"), "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, VRCFuryEditorUtils.Rev("retartenep"), "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, VRCFuryEditorUtils.Rev("eloh"), "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"[_\-.\[\]\(\)]+", " ");
            name = LowerCaseSequentialUpperCaseChars(name);
            name = Regex.Replace(name, @"(\B[A-Z])", " $1");
            name = Regex.Replace(name, @" +", " ");
            name = name.ToLower();
            name = name.Trim();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name);
        }

        private static string LowerCaseSequentialUpperCaseChars(string str) {
            var arr = str.ToCharArray();
            var lastWasUpper = false;
            for (var i = 0; i < arr.Length; i++) {
                var currentIsUpper = Char.IsUpper(arr[i]);
                if (lastWasUpper && currentIsUpper) {
                    arr[i - 1] = Char.ToLower(arr[i - 1]);
                    arr[i] = Char.ToLower(arr[i]);
                }
                lastWasUpper = currentIsUpper;
            }
            return new string(arr);
        }

        public static bool IsDirectChildOfHips(VFGameObject obj) {
            return IsChildOfBone(obj, HumanBodyBones.Hips)
                   && !IsChildOfBone(obj, HumanBodyBones.Chest)
                   && !IsChildOfBone(obj, HumanBodyBones.Spine)
                   && !IsChildOfBone(obj, HumanBodyBones.LeftUpperArm)
                   && !IsChildOfBone(obj, HumanBodyBones.LeftUpperLeg)
                   && !IsChildOfBone(obj, HumanBodyBones.RightUpperArm)
                   && !IsChildOfBone(obj, HumanBodyBones.RightUpperLeg);
        }

        public static bool IsChildOfHead(VFGameObject obj) {
            return IsChildOfBone(obj, HumanBodyBones.Head, false);
        }

        public static bool IsChildOfBone(VFGameObject obj, HumanBodyBones bone, bool followConstraints = true) {
            try {
                VFGameObject avatarObject = VRCAvatarUtils.GuessAvatarObject(obj);
                if (!avatarObject) return false;
                var boneObj = VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, bone);
                return boneObj && IsChildOf(boneObj, obj, followConstraints);
            } catch (Exception) {
                return false;
            }
        }

        private static bool IsChildOf(Transform parent, Transform child, bool followConstraints) {
            var alreadyChecked = new HashSet<Transform>();
            var current = child;
            while (current != null) {
                alreadyChecked.Add(current);
                if (current == parent) return true;
                if (followConstraints) {
                    Transform foundConstraint = null;
                    foreach (var constraint in current.GetComponents<IConstraint>()) {
                        if (!(constraint is ParentConstraint) && !(constraint is PositionConstraint)) continue;
                        if (constraint.sourceCount == 0) continue;
                        var source = constraint.GetSource(0).sourceTransform;
                        if (source != null && !alreadyChecked.Contains(source)) {
                            foundConstraint = source;
                            break;
                        }
                    }

                    if (foundConstraint) {
                        current = foundConstraint;
                        continue;
                    }
                }
                current = current.parent;
            }
            return false;
        }
    }
}
