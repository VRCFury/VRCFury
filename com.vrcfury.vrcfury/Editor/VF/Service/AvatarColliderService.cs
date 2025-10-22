using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using VF.Builder;
using VF.Hooks;
using VF.Injector;
using VF.Model.Feature;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Service {
    [VFService]
    internal class AvatarColliderService {
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly GlobalsService globals;

        private readonly Lazy<IDictionary<String, FoundCollider>> all;

        public AvatarColliderService() {
            all = new Lazy<IDictionary<string, FoundCollider>>(MakeAll);
        }

        private class FoundCollider {
            public string name;
            public Action<Func<VRCAvatarDescriptor.ColliderConfig, VRCAvatarDescriptor.ColliderConfig>> mutate;
            public bool wasRealAvatarBone;
            public bool customizedByOtherPlugin;
            public bool customizedByVrcf;
            public bool isFinger;
        }

        private IDictionary<String, FoundCollider> MakeAll() {
            if (OriginalContactsHook.fixException != null) {
                throw OriginalContactsHook.fixException;
            }
            return avatar.GetType().GetFields()
                .Where(f => f.FieldType == typeof(VRCAvatarDescriptor.ColliderConfig))
                .ToImmutableDictionary(
                    f => f.Name,
                    f => {
                        var collider = (VRCAvatarDescriptor.ColliderConfig)f.GetValue(avatar);
                        return new FoundCollider {
                            name = f.Name,
                            mutate = mutator => {
                                var c = (VRCAvatarDescriptor.ColliderConfig)f.GetValue(avatar);
                                f.SetValue(avatar, mutator(c));
                            },
                            wasRealAvatarBone =
                                collider.state != VRCAvatarDescriptor.ColliderConfig.State.Disabled
                                && collider.transform != null
                                && OriginalContactsHook.usedTransforms.Contains(collider.transform),
                            customizedByOtherPlugin =
                                collider.state != VRCAvatarDescriptor.ColliderConfig.State.Disabled
                                && collider.transform != null
                                && !OriginalContactsHook.usedTransforms.Contains(collider.transform),
                            customizedByVrcf = false,
                            isFinger = f.Name.Contains("finger")
                        };
                    });
        }

        public string GetNextFinger() {
            // Always use disabled fingers first
            foreach (var found in all.Value.Values) {
                if (!found.isFinger) continue;
                if (found.wasRealAvatarBone) continue;
                if (found.customizedByOtherPlugin) continue;
                if (found.customizedByVrcf) continue;
                return found.name;
            }

            var fingers = new List<string>();
            // If user has a real little finger, steal ring first, otherwise steal middle first
            if (all.Value.TryGetValue(nameof(VRCAvatarDescriptor.collider_fingerLittleL), out var little) && little.wasRealAvatarBone) {
                fingers.AddRange(new[] {
                    nameof(VRCAvatarDescriptor.collider_fingerRingL),
                    nameof(VRCAvatarDescriptor.collider_fingerRingR)
                });
            }
            fingers.AddRange(new [] {
                nameof(VRCAvatarDescriptor.collider_fingerMiddleL),
                nameof(VRCAvatarDescriptor.collider_fingerMiddleR),
                nameof(VRCAvatarDescriptor.collider_fingerRingL),
                nameof(VRCAvatarDescriptor.collider_fingerRingR),
                nameof(VRCAvatarDescriptor.collider_fingerLittleL),
                nameof(VRCAvatarDescriptor.collider_fingerLittleR)
            });
            foreach (var finger in fingers) {
                var found = GetColliderOrNull(finger);
                if (found == null) continue;
                if (found.customizedByOtherPlugin) continue;
                if (found.customizedByVrcf) continue;
                return found.name;
            }

            throw new Exception("Too many VRCF Global Collider components are present on this avatar, ran out of finger colliders to use.");
        }

        [CanBeNull]
        private FoundCollider GetColliderOrNull(string colliderName) {
            return all.Value.TryGetValue(colliderName, out var c) ? c : null;
        }

        public void CustomizeCollider(string colliderName, VFGameObject transform, float radius, float height) {
            var found = GetColliderOrNull(colliderName);
            if (found == null) {
                throw new Exception("Collider does not exist on avatar: " + colliderName);
            }
            if (found.customizedByVrcf) {
                throw new Exception("Collider was already customized by another VRCF component: " + colliderName);
            }
            found.customizedByVrcf = true;

            // Disable mirroring, we don't need to do any mirror math, since OriginalContactsHook would have ensured that that already happened
            // Note, this doesn't actually impact in-game, it just keeps the gui editor from trying to mirror the values
            if (colliderName.EndsWith("L") || colliderName.EndsWith("R")) {
                var baseName = colliderName.Substring(0, colliderName.Length - 1);
                GetColliderOrNull(baseName + "L")?.mutate(c => { c.isMirrored = false; return c; });
                GetColliderOrNull(baseName + "R")?.mutate(c => { c.isMirrored = false; return c; });
            }

            // If we're moving a finger to the head, make sure any head receivers exclude this finger so it doesn't trigger itself
            if (found.isFinger) {
                foreach (var receiver in avatarObject.GetComponentsInSelfAndChildren<VRCContactReceiver>()) {
                    if (receiver.allowSelf) {
                        RemoveFromContactList(receiver.collisionTags, colliderName);
                    }
                }
            }

            found.mutate(collider => {
                collider.state = VRCAvatarDescriptor.ColliderConfig.State.Custom;
                collider.position = Vector3.zero;
                collider.radius = radius;
                collider.rotation = Quaternion.identity;

                if (!found.isFinger) {
                    collider.transform = transform;
                    collider.rotation = Quaternion.Euler(90,0,0);
                    collider.height = height;
                } else {
                    // Vrchat places the capsule for fingers in a very strange place, but essentially it will:
                    // If collider length is 0, it will be a sphere centered on the set transform
                    // If collider length < radius*2, it will be a sphere in a weird in-between location
                    // If collider length >= radius*2, it will be a capsule with one end attached to the set transform's parent,
                    //   facing the direction of the set transform.

                    var childObj = GameObjects.Create("GlobalContact", transform);
                    PhysboneUtils.RemoveFromPhysbones(childObj);
                    globals.addOtherFeature(new ShowInFirstPerson {
                        useObjOverride = true,
                        objOverride = childObj,
                        onlyIfChildOfHead = true
                    });
                    if (height <= radius * 2) {
                        // It's a sphere
                        collider.transform = childObj;
                        collider.height = 0;
                    } else {
                        // It's a capsule
                        childObj.localPosition = new Vector3(0, 0, -height / 2);
                        var directionObj = GameObjects.Create("Direction", childObj);
                        directionObj.localPosition = new Vector3(0, 0, 0.0001f);
                        collider.transform = directionObj;
                        collider.height = height;

                        // Turns out capsules work in game differently than they do in the vrcsdk in the editor
                        // They're even more weird. The capsules in game DO NOT include the endcaps in the height,
                        // and attach the end of the cylinder to the parent (not the endcap).
                        // This fixes them so they work properly in game:
                        var p = childObj.localPosition;
                        p.z += collider.radius;
                        childObj.localPosition = p;
                        collider.height -= collider.radius * 2;
                    }
                }

                return collider;
            });
        }
        
        private static void RemoveFromContactList(List<string> collisionTags, string fingerColliderName) {
            var fingerCollisionTag = fingerColliderName.Replace("collider_", "");
            fingerCollisionTag = fingerCollisionTag.Substring(0, fingerCollisionTag.Length - 1);
            fingerCollisionTag = fingerCollisionTag[0].ToString().ToUpper() + fingerCollisionTag.Substring(1);
            var suffix = fingerColliderName[fingerColliderName.Length - 1];

            if (RemoveFromList(collisionTags, "Finger")) {
                AddToList(collisionTags, "FingerIndex");
                AddToList(collisionTags, "FingerMiddle");
                AddToList(collisionTags, "FingerRing");
                AddToList(collisionTags, "FingerLittle");
            }
            
            if (RemoveFromList(collisionTags, "Finger" + suffix)) {
                AddToList(collisionTags, "FingerIndex" + suffix);
                AddToList(collisionTags, "FingerMiddle" + suffix);
                AddToList(collisionTags, "FingerRing" + suffix);
                AddToList(collisionTags, "FingerLittle" + suffix);
            }
            
            if (RemoveFromList(collisionTags, fingerCollisionTag)) {
                AddToList(collisionTags, fingerCollisionTag + "L");
                AddToList(collisionTags, fingerCollisionTag + "R");
            }

            RemoveFromList(collisionTags, fingerCollisionTag + suffix);
        }

        private static bool RemoveFromList(List<string> list, string element) {
            return list.RemoveAll(e => e == element) > 0;
        }
        private static void AddToList(List<string> list, string element) {
            RemoveFromList(list, element);
            list.Add(element);
        }
    }
}
