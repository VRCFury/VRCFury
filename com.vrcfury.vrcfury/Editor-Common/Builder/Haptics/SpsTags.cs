namespace VF.Builder.Haptics {
    internal static class SpsTags {
        public const string Prefix = "SPS/";
        public const string PublicChannel = "_public";
        public const string NotHipsChannel = "_nothips";

        // SPS3 Plug
        // At the plug base, +Z toward the tip, radius = plug length
        // If ScaleV1: X = world radius at socket entrance, Z = world length
        public static string FormatPlug(string channel) => $"{Prefix}Plug/{channel}";

        // SPS3 Socket
        // At the entrance, +Z outward, radius = 0
        // If ScaleV1: X =
        //   Bits 0–6: group ID (1–127)
        //   Bits 7–9: path index
        //   Bit 10: UnlockAll
        //   Bit 11: UnlockLocalX
        //   Bit 12: RadiusOffset
        //   Bit 13: Twoway
        //   Bit 14: Collaps
        // For entrance:
        public static string FormatSocket(string channel) => $"{Prefix}Socket/{channel}";
        // For path stops after entrance:
        public static string FormatSocketPath(string channel) => $"{Prefix}SocketPath/{channel}";

        // This tag means that socket/plug scale data v1 is available
        // It can also be detected by 0.002 < scale Y < 0.003; version = round(Y * 10000) - 20
        public const string ScaleV1 = "SPS/Flag/ScaleV1";
        // Socket triggers plug to collapse (hole)
        public const string SocketCollapse = "SPS/Flag/Collapse";
        // Socket can be entered from both sides
        public const string SocketTwoway = "SPS/Flag/Twoway";
        // Socket uses radius offset
        public const string SocketRadiusOffset = "SPS/Flag/RadiusOffset";
        // Socket X axis points toward plug
        public const string SocketUnlockLocalX = "SPS/Flag/UnlockLocalX";
        // Socket points toward plug
        public const string SocketUnlockAll = "SPS/Flag/UnlockAll";

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
        // Scale has no encoding guarantee without SPS3 tags or the scale sentinel
        public const string LegacySocket = "TPS_Orf_Root";
        // Already-built SPS2/later SPS1 plug depth/velocity receivers require this SPS-aligned socket entrance tag
        // Entrance and +Z outward are reliable; imported light pairs determine forward, but baking chooses roll; scale may be unencoded
        public const string LegacySocket2 = "SPSLL_Socket_Root";

        // Removed: Poiyomi TPS/early VRCFury orientation tests required Root-to-Front; contact-based TPS activation is unsupported
        // public const string LegacySocketFront = "TPS_Orf_Norm";
        // Removed: only the retired experimental SPS1 triangulation consumed this front marker
        // public const string TagSpsSocketFront = "SPSLL_Socket_Front";
        // Removed: SPS1 Plus/triangulation used ring classification; retained .4106/.4206 lights bypass that decoder
        // public const string TagSpsSocketIsRing = "SPSLL_Socket_Ring";
        // Removed: SPS1 Plus/triangulation used hole classification; retained .4106/.4206 lights bypass that decoder
        // public const string TagSpsSocketIsHole = "SPSLL_Socket_Hole";
    }
}
