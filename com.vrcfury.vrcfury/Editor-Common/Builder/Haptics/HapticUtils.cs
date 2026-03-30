using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using VF.Inspector;
using VF.Utils;
using Random = System.Random;

namespace VF.Builder.Haptics {
    internal static class HapticUtils {
        public const string CONTACT_PEN_MAIN = "TPS_Pen_Penetrating";
        public const string CONTACT_PEN_WIDTH = "TPS_Pen_Width";
        public const string CONTACT_PEN_CLOSE = "TPS_Pen_Close";
        public const string CONTACT_PEN_ROOT = "TPS_Pen_Root";
        public const string TagTpsOrfRoot = "TPS_Orf_Root";
        public const string TagTpsOrfFront = "TPS_Orf_Norm";

        public const string TagSpsSocketRoot = "SPSLL_Socket_Root";
        public const string TagSpsSocketFront = "SPSLL_Socket_Front";
        public const string TagSpsSocketIsRing = "SPSLL_Socket_Ring";
        public const string TagSpsSocketIsHole = "SPSLL_Socket_Hole";

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

        public enum ReceiverParty {
            Self,
            Others
        }

        private static bool IsZeroScale(VFGameObject obj) {
            var scale = obj.localScale;
            return scale.x == 0 || scale.y == 0 || scale.z == 0;
        }
        private static bool IsNegativeScale(VFGameObject obj) {
            var scale = obj.localScale;
            return scale.x < 0 || scale.y < 0 || scale.z < 0;
        }
        public static bool IsNonUniformScale(VFGameObject obj) {
            var scale = obj.localScale;
            return Math.Abs(scale.x - scale.y) / scale.x > 0.05
                   || Math.Abs(scale.x - scale.z) / scale.x > 0.05;
        }
        public static bool AssertValidScale(VFGameObject obj, string type, bool shouldThrow = true) {
            var current = obj;
            while (true) {
                if (IsZeroScale(current)) {
                    if (shouldThrow) throw new Exception(
                        "A haptic component exists on an object with zero scale." +
                        " This object must not be zero scale or size calculation will fail.\n\n" +
                        "Component path: " + obj.GetPath() + "\n" +
                        "Offending object: " + current.GetPath());
                    return false;
                }
                if (IsNegativeScale(current)) {
                    if (shouldThrow) throw new Exception(
                        "A haptic component exists on an object with negative scale." +
                        " This object must have a positive scale or size calculation will fail.\n\n" +
                        "Component path: " + obj.GetPath() + "\n" +
                        "Offending object: " + current.GetPath());
                    return false;
                }
                if (IsNonUniformScale(current)) {
                    var bypass = obj.Find("ItsOkayThatOgbMightBeBroken") != null;
                    if (!bypass) {
                        if (shouldThrow) throw new Exception(
                            "A haptic component exists on an object with a non-uniform scale." +
                            " This object (and all parents) must have an X, Y, and Z scale value that match" +
                            " each other, or size calculation will fail.\n\n" +
                            "Component path: " + obj.GetPath() + "\n" +
                            "Offending object: " + current.GetPath());
                        return false;
                    }
                }

                var parent = current.parent;
                if (parent == null) break;
                current = parent;
            }

            return true;
        }

        public static VFGameObject GetMeshRoot(Renderer r) {
            if (r is SkinnedMeshRenderer skin && skin.rootBone != null) {
                return skin.rootBone;
            }
            return r.owner();
        }

        private static string GetActualId<T>(IEnumerable<T> items, T target, Func<T, string> getPreferredId) {
            var usedNames = new HashSet<string>();
            foreach (var item in items) {
                var name = MakeUniqueId(usedNames, getPreferredId(item));
                if (Equals(item, target)) {
                    return name;
                }
            }

            return getPreferredId(target);
        }
        
        public static string GetFallbackId(VFGameObject obj) {
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

        public static string GetActualId<T>(
            T target,
            VFGameObject owner,
            Func<VFGameObject, IEnumerable<T>> getItems,
            Func<T, string> getPreferredId
        ) {
            string PreferredId(T item) => getPreferredId(item);
            return GetActualId(owner.uploadRoots.SelectMany(getItems), target, PreferredId);
        }

        public static string MakeUniqueId(ISet<string> usedIds, string prefix) {
            for (var i = 0; ; i++) {
                var next = prefix + (i == 0 ? "" : i + "");
                if (usedIds.Contains(next)) continue;
                usedIds.Add(next);
                return next;
            }
        }

        public static string GetPreferredId<T>(
            T component,
            Func<T, string> getExplicitId,
            Func<T, string> getFallbackId
        ) {
            var id = getExplicitId(component);
            if (string.IsNullOrWhiteSpace(id)) {
                id = getFallbackId(component);
            }
            return id;
        }

        private static string NormalizeName(string name) {
            name = Regex.Replace(name, @"ezdps_([a-z][a-z]?_?)?", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"dps", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"sps", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"gameobject", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"object", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"armature", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"tps", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"haptic", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"socket", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"plug", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"ring", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"hole", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"mesh", "", RegexOptions.IgnoreCase);
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
    }
}
