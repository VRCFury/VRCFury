#if VRCSDK_HAS_NAME_BASED_PARAMETER_SYNC
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using NUnit.Framework;
using UnityEngine;
using VF.Builder;
using VF.Service;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.Api;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class FreshAvatarUploadTest {
        private static VRCSdkControlPanelAvatarBuilder builder;
        private static string reservedId;
        private static Action<GameObject> onBuild;

        [Test]
        public void FreshPublishBuildUsesTheIdReturnedByTheServer() {
            var avatar = new GameObject("Fresh upload");
            var previousBuildType = VRC_SdkBuilder.ActiveBuildType;
            var patches = new Harmony("com.vrcfury.tests.fresh-avatar-upload");
            try {
                avatar.AddComponent<VRCAvatarDescriptor>();
                var pipeline = avatar.GetComponent<PipelineManager>() ?? avatar.AddComponent<PipelineManager>();
                Assert.That(pipeline.blueprintId, Is.Null.Or.Empty);
                builder = new VRCSdkControlPanelAvatarBuilder();
                reservedId = "avtr_" + Guid.NewGuid().ToString();
                string idAtBuild = null;
                string nameAtBuild = null;
                onBuild = target => {
                    var clone = Object.Instantiate(target);
                    try {
                        var injector = VRCFuryInjectorBuilder.GetInjector(clone.GetComponent<VRCAvatarDescriptor>());
                        idAtBuild = injector.GetService<PipelineIdService>().Get();
                        injector.GetService<ControllersService>().ClearCache(applyBaseMask: false);
                        nameAtBuild = injector.GetService<UniqueParamService>().GetAvatarWideName("SecurityLock");
                    } finally {
                        Object.DestroyImmediate(clone);
                    }
                };

                // Exercise the SDK's real fresh-upload orchestration and ID assignment offline.
                // Replace the account/UI checks, record API and bundle I/O only for this test's upload.
                Patch(typeof(VRCApi), nameof(VRCApi.CreateAvatarRecord), nameof(CreateRecord));
                Patch(typeof(VRCApi), nameof(VRCApi.DeleteAvatar), nameof(DeleteRecord));
                Patch(builder.GetType(), "VerifyUploadPermissions", nameof(CompleteUiStep));
                Patch(builder.GetType(), "CheckCopyrightAgreement", nameof(CompleteUiStep));
                Patch(builder.GetType(), "Build", nameof(CaptureBuild));
                Patch(builder.GetType(), "Upload", nameof(FinishUpload));

                using var cancellation = new CancellationTokenSource();
                var publish = builder.BuildAndUpload(
                    avatar,
                    new VRCAvatar { Name = reservedId },
                    cancellationToken: cancellation.Token
                );
                Assert.That(publish.IsCompleted, Is.True, "All offline upload steps should complete synchronously.");
                publish.GetAwaiter().GetResult();
                Assert.That(idAtBuild, Is.EqualTo(reservedId), "The SDK entered the build before assigning the reserved ID.");
                Assert.That(nameAtBuild, Is.EqualTo("VF_" + UniqueParamService.Hash(reservedId) + "_SecurityLock"));
            } finally {
                patches.UnpatchAll(patches.Id);
                VRC_SdkBuilder.ActiveBuildType = previousBuildType;
                Object.DestroyImmediate(avatar);
                onBuild = null;
                builder = null;
                reservedId = null;
            }

            void Patch(Type owner, string method, string prefix) {
                // Build and Upload also have public convenience overloads. Hook the SDK's private bundle methods.
                var flags = owner == typeof(VRCApi)
                    ? BindingFlags.Public | BindingFlags.Static
                    : BindingFlags.NonPublic | BindingFlags.Instance;
                var original = owner.GetMethod(method, flags) ?? throw new MissingMethodException(owner.FullName, method);
                patches.Patch(original, prefix: new HarmonyMethod(typeof(FreshAvatarUploadTest), prefix));
            }
        }

        private static bool CreateRecord(VRCAvatar data, ref Task<VRCAvatar> __result) {
            if (data.Name != reservedId) return true;
            __result = Task.FromResult(new VRCAvatar { ID = reservedId });
            return false;
        }

        private static bool DeleteRecord(string id, ref Task<VRCAvatar> __result) {
            if (id != reservedId) return true;
            __result = Task.FromResult(default(VRCAvatar));
            return false;
        }

        private static bool CompleteUiStep(VRCSdkControlPanelAvatarBuilder __instance, ref Task __result) {
            if (__instance != builder) return true;
            __result = Task.CompletedTask;
            return false;
        }

        private static bool CaptureBuild(VRCSdkControlPanelAvatarBuilder __instance, GameObject target, ref Task<string> __result) {
            if (__instance != builder) return true;
            onBuild(target);
            __result = Task.FromResult("test-bundle");
            return false;
        }

        private static bool FinishUpload(VRCSdkControlPanelAvatarBuilder __instance, ref Task<bool> __result) {
            if (__instance != builder) return true;
            __result = Task.FromResult(true);
            return false;
        }
    }
}
#endif
