using System;
using System.Collections.Generic;
using System.Text;
using VF.Exceptions;
using VF.Injector;
using VF.Utils;

namespace VF.Service {
    // Reserves unused names. Allocating a name does not create an animator or expression parameter.
    [VFService]
    internal class UniqueParamService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly ExpressionParamsService expressionParams;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly PipelineIdService pipelineId;

        private readonly Dictionary<string, string> reserved = new Dictionary<string, string>();
        private readonly Dictionary<string, string> objectHashes = new Dictionary<string, string>();
        private readonly string buildHash = Hash(Guid.NewGuid().ToString("N"));
        private string avatarHash;
        private string AvatarHash => avatarHash ??= Hash(pipelineId.Get());

        public string GetInternalName(string name) => Reserve(name, buildHash);

        public string GetAvatarWideName(string name) => Reserve(name, AvatarHash, requireExactName: true);

        public string GetObjectScopedName(VFGameObject owner, string name) {
            var path = owner.GetPath(avatarObject);
            var objectHash = objectHashes.GetOrCreate(path, () => Hash(pipelineId.Get() + "\0" + path));
            return Reserve(name, objectHash);
        }

        private string Reserve(string name, string hash, bool requireExactName = false) {
            // Re-scoping a generated parameter should retain its readable name.
            if (reserved.TryGetValue(name, out var original)) name = original;
            string Format(string candidate) => $"VF_{hash}_{candidate}";
            var unique = name.MakeUnique(candidate => IsUsed(Format(candidate)));
            if (requireExactName && unique != name) {
                throw new VRCFBuilderException($"The avatar-wide VRCFury parameter '{Format(name)}' is already in use.");
            }
            var result = Format(unique);
            reserved.Add(result, unique);
            return result;
        }

        internal static string Hash(string key) {
            // FNV-1a over UTF-8, encoded in base 62 at a fixed width.
            const string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            ulong hash = 14695981039346656037UL;
            foreach (var b in Encoding.UTF8.GetBytes(key ?? "")) {
                hash = unchecked((hash ^ b) * 1099511628211UL);
            }
            var chars = new char[8];
            for (int i = chars.Length - 1; i >= 0; i--) {
                chars[i] = alphabet[(int)(hash % 62)];
                hash /= 62;
            }
            return new string(chars);
        }

        private bool IsUsed(string name) {
            if (reserved.ContainsKey(name)) return true;
            if (expressionParams.Contains(name)) return true;
            foreach (var controller in controllers.GetAllUsedControllers()) {
                if (controller.TryGetParam(name, out _)) return true;
            }
            return false;
        }
    }
}
