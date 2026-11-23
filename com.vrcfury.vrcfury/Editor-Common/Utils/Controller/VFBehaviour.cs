using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VF.Builder;
using Object = UnityEngine.Object;

namespace VF.Utils.Controller {
    internal class VFBehaviour {
        internal static Action<StateMachineBehaviour, Func<string, string>, bool> rewriteParameters;
        internal static Func<StateMachineBehaviour, string> getObjectPath;
        internal static Action<StateMachineBehaviour, string> setObjectPath;
        internal static Func<StateMachineBehaviour, IEnumerable<Object>> getReferencedAssets;

        private static long nextIdentity = 1;

        private readonly long identity;
        private readonly StateMachineBehaviour raw;
        private VFResolvedObject? objectPath;

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
            var path = getObjectPath?.Invoke(behaviour.raw);
            if (path == null) return behaviour;

            behaviour.objectPath = VFResolvedObject.Load(path, context, typeof(GameObject));
            return behaviour.objectPath.HasValue ? behaviour : null;
        }

        internal VFBehaviour Clone() {
            var clone = new VFBehaviour(identity, raw);
            clone.objectPath = objectPath;
            return clone;
        }

        internal long Identity => identity;

        private static StateMachineBehaviour CloneRaw(StateMachineBehaviour source) {
            return source?.Clone();
        }

        internal T Read<T>() where T : StateMachineBehaviour {
            return raw as T;
        }

        internal VFGameObject GetObjectPathTarget() => objectPath?.target;

        internal void Edit<T>(Action<T> action) where T : StateMachineBehaviour {
            if (action == null) return;
            var typed = Read<T>();
            if (typed == null) return;
            action(typed);
        }

        internal void RewriteParameters(Func<string, string> rewriteParamName, bool includeWrites) {
            if (rewriteParamName != null) rewriteParameters?.Invoke(raw, rewriteParamName, includeWrites);
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
                    objectPath = objectPath
                };
            }
        }

        internal StateMachineBehaviour Save(VFSaveContext context) {
            var output = CloneRaw(raw);
            if (objectPath.HasValue) {
                setObjectPath?.Invoke(output, objectPath.Value.GetPath(context.BindingRoot));
            }
            foreach (var asset in getReferencedAssets?.Invoke(output) ?? Array.Empty<Object>()) {
                context.AddOtherAsset(asset);
            }
            context.AddNewAsset(output);
            return output;
        }
    }
}
