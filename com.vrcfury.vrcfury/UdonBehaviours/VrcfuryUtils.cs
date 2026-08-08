using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

namespace com.vrcfury.udon {
    internal static class VrcfuryUtils {
        public static int AddToList<T>(ref T[] list, T item) {
            for (var i = 0; i < list.Length; i++) {
                if (Equals(list[i], item)) return i;
            }
            for (var i = 0; i < list.Length; i++) {
                if (Equals(list[i], default(T))) {
                    list[i] = item;
                    return i;
                }
            }

            var id = list.Length;
            var newItems = new T[id + 1];
            for (var i = 0; i < id; i++) {
                newItems[i] = list[i];
            }
            newItems[id] = item;
            list = newItems;
            return id;
        }

        public static void RemoveFromList<T>(this T[] list, int id) {
            if (id < 0 || id >= list.Length) return;
            list[id] = default(T);
        }

        public static int IndexOf<T>(this T[] list, T item) {
            for (var i = 0; i < list.Length; i++) {
                if (Equals(list[i], item)) return i;
            }
            return -1;
        }

        public static ContactSenderProxy GetMatchingContact(
            ContactSenderProxy contact,
            ContactSenderProxy[] candidates
        ) {
            var nearestDistance = 0.01f * 0.01f;
            ContactSenderProxy matchingContact = null;
            for (var i = 0; i < candidates.Length; i++) {
                var candidate = candidates[i];
                if (candidate == null || !candidate.isValid) continue;
                if (candidate.player != contact.player) continue;
                var distance = (candidate.position - contact.position).sqrMagnitude;
                if (distance > nearestDistance) continue;
                nearestDistance = distance;
                matchingContact = candidate;
            }
            return matchingContact;
        }

        public static ContactSenderProxy GetNearestSender(
            VRCContactReceiver receiver,
            ContactSenderProxy[] candidates
        ) {
            var nearestProximity = 0f;
            ContactSenderProxy nearest = null;
            for (var i = 0; i < candidates.Length; i++) {
                var candidate = candidates[i];
                if (candidate == null || !candidate.isValid) continue;
                var proximity = receiver.CalculateProximity(candidate);
                if (proximity <= nearestProximity) continue;
                nearestProximity = proximity;
                nearest = candidate;
            }
            return nearest;
        }
    }
}
