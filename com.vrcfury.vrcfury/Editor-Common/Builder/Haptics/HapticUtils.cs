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

        public const int GroupCount = 127;
        public const int MaxPathNodes = 5; // Matches SPS2's SPS_CHAIN_MAX_SOCKETS, including the entrance.
        public const float ScaleVersion = 0.0021f;

        [Flags]
        public enum SocketScaleFlags {
            None = 0,
            UnlockAll = 1 << 10,
            UnlockLocalX = 1 << 11,
            RadiusOffset = 1 << 12,
            Twoway = 1 << 13,
            Collapse = 1 << 14
        }

        public static Vector3 EncodeSocketScale(int group, int pathIndex, SocketScaleFlags flags = SocketScaleFlags.None) {
            if (group < 1 || group > GroupCount) throw new ArgumentOutOfRangeException(nameof(group));
            if (pathIndex < 0 || pathIndex >= 8) throw new ArgumentOutOfRangeException(nameof(pathIndex));
            if (((int)flags & ~0x7C00) != 0) throw new ArgumentOutOfRangeException(nameof(flags));
            // X: group ID 0-6 (nonzero), path index 7-9, flags 10-14.
            // Y identifies the scale format/version; emitted flags are also collision tags.
            return new Vector3(group | (pathIndex << 7) | (int)flags, ScaleVersion, 1);
        }

        public static Vector3 EncodePlugScale(float worldLength, float worldRadius) {
            if (!(worldLength > 0) || float.IsInfinity(worldLength)) {
                throw new ArgumentOutOfRangeException(nameof(worldLength), "Plug length must be finite and positive.");
            }
            if (!(worldRadius > 0) || float.IsInfinity(worldRadius)) {
                throw new ArgumentOutOfRangeException(nameof(worldRadius), "Encoded plug radius must be finite and positive.");
            }
            // A Y-only world-scale constraint keeps the version sentinel fixed while X/Z follow resizing.
            return new Vector3(Mathf.Min(worldRadius, worldLength), ScaleVersion, worldLength);
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
            var nonUniformScales = new List<string>();
            while (true) {
                if (IsZeroScale(current)) {
                    if (shouldThrow) throw new Exception(
                        "SPS cannot be used on an object with zero scale.\n\n" +
                        "Component path: " + obj.GetDebugPath() + "\n" +
                        "Offending object: " + current.GetDebugPath());
                    return false;
                }
                if (IsNegativeScale(current)) {
                    if (shouldThrow) throw new Exception(
                        "SPS cannot be used on an object with negative scale.\n\n" +
                        "Component path: " + obj.GetDebugPath() + "\n" +
                        "Offending object: " + current.GetDebugPath());
                    return false;
                }
                if (IsNonUniformScale(current)) {
                    var scale = current.localScale;
                    nonUniformScales.Add($"{current.GetDebugPath()}: ({scale.x}, {scale.y}, {scale.z})");
                }

                var parent = current.parent;
                if (parent == null) break;
                current = parent;
            }

            var bypass = obj.Find("ItsOkayThatOgbMightBeBroken") != null;
            if (nonUniformScales.Any() && !bypass) {
                if (shouldThrow) throw new Exception(
                    "SPS cannot be used on an object with non-uniform scale." +
                    " This object (and all parents) must have a scale where X=Y=Z, such as 1,1,1, or 1.5,1.5,1.5, etc.\n\n" +
                    "Component path: " + obj.GetDebugPath() + "\n" +
                    "Offending objects:\n" + nonUniformScales.Join('\n'));
                return false;
            }

            return true;
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
            var id = prefix.MakeUnique(usedIds);
            usedIds.Add(id);
            return id;
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
