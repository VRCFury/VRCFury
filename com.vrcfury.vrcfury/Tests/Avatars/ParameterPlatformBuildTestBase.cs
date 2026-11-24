using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VF.Builder;
using VF.Exceptions;
using VF.Injector;
using VF.Service;
using VF.Service.Compressor;
using VF.Utils;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal abstract class ParameterPlatformBuildTestBase {
        protected class Build : IDisposable {
            public readonly GameObject root;
            public readonly VRCFuryInjector injector;
            public readonly ExpressionParamsService expressions;
            public readonly ParameterPlatformAlignmentService alignment;
            public readonly VFAvatarController fx;

            public Build(string rootName, string blueprint = "avtr-example") {
                root = new GameObject(rootName);
                var avatar = root.AddComponent<VRCAvatarDescriptor>();
                avatar.baseAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.FX }
                };
                avatar.specialAnimationLayers = new[] {
                    new VRCAvatarDescriptor.CustomAnimLayer { type = VRCAvatarDescriptor.AnimLayerType.Action }
                };
                var pipeline = root.GetComponent<PipelineManager>() ?? root.AddComponent<PipelineManager>();
                pipeline.blueprintId = blueprint;
                injector = VRCFuryInjectorBuilder.GetInjector(avatar);
                var controllers = injector.GetService<ControllersService>();
                controllers.ClearCache(applyBaseMask: false);
                fx = controllers.GetFx();
                expressions = injector.GetService<ExpressionParamsService>();
                alignment = injector.GetService<ParameterPlatformAlignmentService>();
            }

            public string Add(string path, string name, bool synced = true) {
                VFGameObject owner = root;
                foreach (var part in path.Split('/').Where(part => part.Length > 0)) {
                    owner = owner.Find(part) ?? GameObjects.Create(part, owner);
                }
                var param = fx.GetOrCreateBool(injector.GetService<UniqueParamService>().GetObjectScopedName(owner, name));
                param.MakeExpressionBool(synced: synced);
                return param.name;
            }

            public OptimizationDecision Align(ParameterPlatformAlignmentService.SavedData desktop) {
                var parameters = expressions.GetSnapshot().ToList();
                var result = ParameterPlatformAlignmentService.AlignForMobile(parameters, desktop);
                expressions.SetParameters(parameters);
                return result.decision;
            }

            public void Dispose() => Object.DestroyImmediate(root);
        }
    }
}
