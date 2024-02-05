using System.Collections.Generic;

namespace VF.Upgradeable {
    public static class IUpgradeableUtility {
        public static void IUpgradeableOnAfterDeserialize(this IUpgradeable upgradeable) {
            if (upgradeable.Version < 0) {
                // Object was deserialized, but had no version. Default to version 0.
                upgradeable.Version = 0;
            }
        }

        public static void IUpgradeableOnBeforeSerialize(this IUpgradeable upgradeable) {
            if (upgradeable.Version < 0) {
                // Object was created fresh (not deserialized), so it's automatically the newest
                upgradeable.Version = upgradeable.GetLatestVersion();
            }
        }

        private static bool UpgradeOne(this IUpgradeable upgradeable) {
            var fromVersion = upgradeable.Version;
            if (fromVersion < 0) fromVersion = upgradeable.GetLatestVersion();
            var latestVersion = upgradeable.GetLatestVersion();
            
            var changedSomething = upgradeable.Upgrade(fromVersion);

            if (fromVersion < latestVersion) {
                upgradeable.Version = latestVersion;
                changedSomething = true;
            }

            return changedSomething;
        }

        public static bool UpgradeRecursive(object root) {
            var list = new List<IUpgradeable>();
            UnitySerializationUtils.Iterate(root, visit => {
                if (visit.value is IUpgradeable upgradeable) {
                    list.Add(upgradeable);
                }
                return UnitySerializationUtils.IterateResult.Continue;
            });
            list.Reverse();
            var upgradedOne = false;
            foreach (var u in list) {
                upgradedOne |= u.UpgradeOne();
            }
            return upgradedOne;
        }

        public static bool IsTooNew(object root) {
            var tooNew = false;
            UnitySerializationUtils.Iterate(root, visit => {
                if (visit.value is IUpgradeable upgradeable) {
                    tooNew |= upgradeable.Version > upgradeable.GetLatestVersion();
                }
                return UnitySerializationUtils.IterateResult.Continue;
            });
            return tooNew;
        }
    }
}
