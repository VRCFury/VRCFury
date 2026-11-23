using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Hooks.Av3EmuFixes {
    /**
     * Marks default senders created by Av3Emulator so Play Mode performance scans can exclude them.
     * This also supports Av3Emulator versions that do not set DontSaveInBuild themselves.
     *
     * https://github.com/lyuma/Av3Emulator/pull/231
     */
    internal static class Av3EmuMarkGeneratedContactsHook {
        [ReflectionHelperOptional]
        private abstract class Reflection : ReflectionHelper {
            private const string Extractor = "Lyuma.Av3Emulator.Runtime.DescriptorCollidersSendersHelper";
            private const string Extract = "ExtractDescriptorColliders";

            public static readonly HarmonyUtils.PatchObj Before = HarmonyUtils.Patch(
                typeof(Av3EmuMarkGeneratedContactsHook),
                nameof(CaptureSenders),
                Extractor,
                Extract
            );
            public static readonly HarmonyUtils.PatchObj After = HarmonyUtils.Patch(
                typeof(Av3EmuMarkGeneratedContactsHook),
                nameof(MarkGeneratedSenders),
                Extractor,
                Extract,
                HarmonyUtils.PatchMode.Postfix
            );
        }

        [VFInit]
        private static void Init() {
            if (!ReflectionHelper.IsReady<Reflection>()) return;
            Reflection.Before.apply();
            Reflection.After.apply();
        }

        private static void CaptureSenders(VRCAvatarDescriptor __0, out HashSet<VRCContactSender> __state) {
            __state = Application.isPlaying && __0 != null
                ? __0.GetComponentsInChildren<VRCContactSender>(true).ToHashSet()
                : null;
        }

        private static void MarkGeneratedSenders(VRCAvatarDescriptor __0, HashSet<VRCContactSender> __state) {
            if (__state == null || __0 == null) return;
            foreach (var sender in __0.GetComponentsInChildren<VRCContactSender>(true)) {
                if (!__state.Contains(sender)) sender.hideFlags |= HideFlags.DontSaveInBuild;
            }
        }
    }
}
