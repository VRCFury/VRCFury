using System;

namespace VF.Builder.Haptics {
    internal static class SpsTags {
        public const int MaxUserChannels = 2;
        public const int MaxUserPathStops = 4;

        [Flags]
        public enum SocketScaleFlagsX {
            None = 0,
            // Bits 0–6 are the group ID. Bits 7–9 are the path index.
            UnlockAll = 1 << 10,
            UnlockLocalX = 1 << 11,
            RadiusOffset = 1 << 12,
            Twoway = 1 << 13,
            Collapse = 1 << 14
        }

        [Flags]
        public enum SocketScaleFlagsZ {
            None = 0,
            // Bit 0 is always set to keep the transform scale positive.
            // OverrideInstantCollapse = 1 << 1, // Use InstantCollapse instead of the shader's default.
            // InstantCollapse = 1 << 2, // Pin the centerline while a hole closes or a ring reopens.
            // Bits 3–13 are reserved and left clear.
            AnimatorHole = 1 << 14 // should be counted as a "hole" for animator purposes (added in 1.1500)
        }

        public const string Prefix = "SPS/";
        public const string PublicChannel = "_public";
        public const string NotHipsChannel = "nothips";

        // SPS3 Plug
        // At the plug base, +Z toward the tip, radius = plug length
        // World scale: X = average radius, Y = radius at the nearest socket, Z = length (meters).
        public static string FormatPlug(string channel) => $"{Prefix}Plug/{channel}";

        // SPS3 Socket
        // At the entrance, +Z outward, radius = 0
        // World scale: Y = 1 (unlimited), or 2 + maximum radius offset in world meters.
        // Z = 1 plus SocketScaleFlagsZ.
        // X = packed word:
        //   Bits 0-6: group ID (1-127)
        //   Bits 7-9: path index
        //   Bit 10: UnlockAll
        //   Bit 11: UnlockLocalX
        //   Bit 12: RadiusOffset
        //   Bit 13: Twoway
        //   Bit 14: Collapse
        // For entrance:
        public static string FormatSocket(string channel) => $"{Prefix}Socket/{channel}";
        // For path stops after entrance:
        public static string FormatSocketPath(string channel) => $"{Prefix}SocketPath/{channel}";

        // SPS3 legacy discovery, SPS1/2 socket depth/haptics, and Poiyomi TPS depth/width
        // SPS1/2/3 use the base position and +Z toward the tip; Poiyomi TPS orientation is not guaranteed
        public const string LegacyPlugSphere = "TPS_Pen_Penetrating";
        // SPS1/2 and Poiyomi TPS derive width by comparing this base sphere with the length-sized sphere
        public const string LegacyPlugWidth = "TPS_Pen_Width";
        // Haptic Touch Receivers and SPS1/2 plug-to-plug proximity use this plug-shaped capsule, regardless of channels
        public const string PlugClose = "TPS_Pen_Close";
        // SPS1/2 socket depth/length and haptic logic use this small base sphere
        public const string LegacyPlugRoot = "TPS_Pen_Root";

        // SPS3 legacy discovery, SPS1/2 proximity/depth/haptics, and Poiyomi TPS orientation/depth use the socket entrance
        // SPS1/2/3 use +Z outward; Poiyomi TPS orientation and very old pre-SPS offset-light entrance positions may differ
        // Scale has no encoding guarantee without SPS3 channel tags
        public const string LegacySocket = "TPS_Orf_Root";
        // Already-built SPS2/later SPS1 plug depth/velocity receivers require this SPS-aligned socket entrance tag
        // Entrance and +Z outward are reliable; imported light pairs determine forward, but baking chooses roll; scale may be unencoded
        public const string LegacySocket2 = "SPSLL_Socket_Root";

        // Optional socket front marker for TPS depth-animation compatibility.
        public const string LegacySocketFront = "TPS_Orf_Norm";
        // Removed: only the retired experimental SPS1 triangulation consumed this front marker
        // public const string TagSpsSocketFront = "SPSLL_Socket_Front";
        // Read-only SPS1/2 classification for SPS3 plugs; new sockets encode this in scale instead
        public const string LegacySocketRing = "SPSLL_Socket_Ring";
        public const string LegacySocketHole = "SPSLL_Socket_Hole";
    }
}
