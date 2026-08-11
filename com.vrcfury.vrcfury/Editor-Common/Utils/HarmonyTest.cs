using System;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace VF.Utils {
    internal static class HarmonyTest {
        private static bool testPatchCalled;
        public static readonly string PatchingError = TestPatching();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TestPatchTarget() {
        }

        private static void TestPatchPrefix() {
            testPatchCalled = true;
        }

        public static string TestPatching() {
            try {
                testPatchCalled = false;
                HarmonyUtils.harmony.Patch(
                    typeof(HarmonyTest).VFStaticMethod(nameof(TestPatchTarget)),
                    prefix: new HarmonyMethod(typeof(HarmonyTest).VFStaticMethod(nameof(TestPatchPrefix)))
                );
                TestPatchTarget();
                if (testPatchCalled) return null;
            } catch (Exception) {
            }
            return "Patching is not supported on this platform";
        }
    }
}
