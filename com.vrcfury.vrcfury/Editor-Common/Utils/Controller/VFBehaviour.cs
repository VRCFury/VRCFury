using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;
using VF.Builder;
using VF.Utils;

namespace VF.Utils.Controller {
    internal class VFBehaviour {
        private abstract class Reflection : ReflectionHelper {
            public static readonly Type VRCAvatarParameterDriver =
                ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDK3.Avatars.Components.VRCAvatarParameterDriver");
            public static readonly FieldInfo DriverParametersField =
                VRCAvatarParameterDriver?.VFField("parameters");
            public static readonly Type DriverParameter =
                DriverParametersField?.FieldType.GetGenericArguments().FirstOrDefault();
            public static readonly FieldInfo DriverParameterName =
                DriverParameter?.VFField("name");
            public static readonly FieldInfo DriverParameterSource =
                DriverParameter?.VFField("source");
            public static readonly FieldInfo DriverParameterType =
                DriverParameter?.VFField("type");
            public static readonly Type VRCAnimatorPlayAudio =
                ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDK3.Avatars.Components.VRCAnimatorPlayAudio");
            public static readonly FieldInfo SourcePath =
                VRCAnimatorPlayAudio?.VFField("SourcePath");
            public static readonly FieldInfo AudioParameterName =
                VRCAnimatorPlayAudio?.VFField("ParameterName");
        }

        private static long nextIdentity = 1;

        private readonly long identity;
        private readonly StateMachineBehaviour raw;
        private VFResolvedObject? audioSource;

        internal VFBehaviour(StateMachineBehaviour raw) {
            if (raw == null) throw new ArgumentNullException(nameof(raw));
            identity = Interlocked.Increment(ref nextIdentity);
            this.raw = CloneRaw(raw);
        }

        private VFBehaviour(
            long identity,
            StateMachineBehaviour raw
        ) {
            this.identity = identity;
            this.raw = CloneRaw(raw);
        }

        internal static VFBehaviour Load(StateMachineBehaviour raw, VFLoadContext context) {
            if (raw == null) return null;
            if (context == null) throw new ArgumentNullException(nameof(context));
            var behaviour = new VFBehaviour(raw);
            return behaviour.InitializeAudioPath(context) ? behaviour : null;
        }

        internal VFBehaviour Clone() {
            var clone = new VFBehaviour(identity, raw);
            clone.audioSource = audioSource;
            return clone;
        }

        internal long Identity => identity;

        private static StateMachineBehaviour CloneRaw(StateMachineBehaviour source) {
            return source?.Clone();
        }

        internal T Read<T>() where T : StateMachineBehaviour {
            return raw as T;
        }

        internal VFGameObject GetAudioPathTarget() {
            return audioSource?.target;
        }

        internal string GetAudioSourcePath() {
            return audioSource?.SourcePath;
        }

        internal VFResolvedObject? GetAudioSource() {
            return audioSource;
        }

        internal void Edit<T>(Action<T> action) where T : StateMachineBehaviour {
            if (action == null) return;
            var typed = Read<T>();
            if (typed == null) return;
            action(typed);
        }

        internal void RewriteParameters(Func<string, string> rewriteParamName, bool includeWrites) {
            if (rewriteParamName == null) return;

            RewriteDriverParameters(rewriteParamName, includeWrites);
            RewriteAudioParameter(rewriteParamName);
        }

        internal IEnumerable<VFBehaviour> Rewrite<T>(Func<VFBehaviour, T, OneOrMany<VFBehaviour>> action) where T : StateMachineBehaviour {
            var typed = Read<T>();
            if (typed == null) {
                yield return this;
                yield break;
            }

            foreach (var rewritten in action(this, typed).Get()) {
                if (rewritten != null) {
                    yield return rewritten;
                }
            }
        }

        internal IEnumerable<VFBehaviour> RewriteRaw<T>(Func<T, OneOrMany<StateMachineBehaviour>> action) where T : StateMachineBehaviour {
            var typed = Read<T>();
            if (typed == null) {
                yield return this;
                yield break;
            }

            foreach (var rewritten in action(typed).Get()) {
                if (rewritten == null) {
                    continue;
                }
                if (ReferenceEquals(rewritten, typed)) {
                    yield return this;
                    continue;
                }

                yield return new VFBehaviour(rewritten) {
                    audioSource = audioSource
                };
            }
        }

        private bool InitializeAudioPath(VFLoadContext context) {
            if (!TryGetAudioSourcePath(raw, out var sourcePath) || sourcePath == null) {
                audioSource = null;
                return true;
            }
            audioSource = VFResolvedObject.Load(sourcePath, context, typeof(GameObject));
            if (!audioSource.HasValue) {
                audioSource = null;
                return false;
            }
            return true;
        }

        private static bool TryGetAudioSourcePath(StateMachineBehaviour raw, out string path) {
            path = null;
            var field = GetAudioSourcePathField(raw);
            if (field == null || field.FieldType != typeof(string)) return false;
            path = (string)field.GetValue(raw);
            return true;
        }

        private static void TrySetAudioSourcePath(StateMachineBehaviour raw, string path) {
            var field = GetAudioSourcePathField(raw);
            if (field == null || field.FieldType != typeof(string)) return;
            field.SetValue(raw, path);
        }

        private static FieldInfo GetAudioSourcePathField(StateMachineBehaviour raw) {
            if (raw == null) return null;
            if (Reflection.SourcePath == null) return null;
            return Reflection.VRCAnimatorPlayAudio?.IsAssignableFrom(raw.GetType()) == true
                ? Reflection.SourcePath
                : null;
        }

        private void RewriteDriverParameters(Func<string, string> rewriteParamName, bool includeWrites) {
            if (Reflection.DriverParametersField == null) return;
            if (Reflection.VRCAvatarParameterDriver?.IsAssignableFrom(raw.GetType()) != true) return;

            if (!(Reflection.DriverParametersField.GetValue(raw) is System.Collections.IEnumerable parameters)) return;
            foreach (var parameter in parameters) {
                if (parameter == null) continue;

                if (includeWrites) {
                    RewriteStringField(parameter, Reflection.DriverParameterName, rewriteParamName);
                }

                if (VFParameterRewriteSettings.ShouldRewriteCopyDriverSources && IsCopyDriverParameter(parameter)) {
                    RewriteStringField(parameter, Reflection.DriverParameterSource, rewriteParamName);
                }
            }
        }

        private void RewriteAudioParameter(Func<string, string> rewriteParamName) {
            if (Reflection.VRCAnimatorPlayAudio?.IsAssignableFrom(raw.GetType()) != true) return;
            RewriteStringField(raw, Reflection.AudioParameterName, rewriteParamName);
        }

        private static bool IsCopyDriverParameter(object parameter) {
            return Reflection.DriverParameterType?.GetValue(parameter)?.ToString() == "Copy";
        }

        private static void RewriteStringField(object target, FieldInfo field, Func<string, string> rewrite) {
            if (target == null || rewrite == null) return;
            if (field != null && field.FieldType == typeof(string)) {
                var value = field.GetValue(target) as string;
                if (!string.IsNullOrEmpty(value)) {
                    field.SetValue(target, rewrite(value));
                }
            }
        }

        internal StateMachineBehaviour Save(VFSaveContext saveContext) {
            var output = CloneRaw(raw);
            if (audioSource.HasValue) {
                TrySetAudioSourcePath(output, audioSource.Value.GetPath(saveContext.BindingRoot));
            }
            saveContext.AddNewAsset(output);
            return output;
        }
    }
}
