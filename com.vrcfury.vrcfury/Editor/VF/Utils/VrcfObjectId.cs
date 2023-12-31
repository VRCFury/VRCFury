using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

namespace VF.Utils {
    public class VrcfObjectId {
        public string guid { get; private set; } = "";
        public long fileId { get; private set; } = 0;
        public string fileName { get; private set; } = "";
        public string objectName { get; private set; } = "";

        public static T IdToObject<T>(string id) where T : Object {
            return FromId(id).ToObject<T>();
        }
        public static string ObjectToId(Object obj) {
            return FromObject(obj).ToId();
        }

        public static VrcfObjectId FromId(string id) {
            var output = new VrcfObjectId();
            if (string.IsNullOrWhiteSpace(id)) return output;
            var split = id.Split('|');

            if (split.Length >= 1) {
                var split2 = split[0].Split(':');
                if (split2.Length >= 1) output.guid = split2[0];
                if (split2.Length >= 2 && long.TryParse(split2[1], out var fileId)) output.fileId = fileId;
            }
            if (split.Length >= 2) output.fileName = split[1];
            if (split.Length >= 3) output.objectName = split[2];
            return output;
        }

        public string ToId() {
            var output = "";

            if (guid == "") return output;
            output = fileId == 0 ? guid : (guid + ":" + fileId);

            if (fileName == "") return output;
            output += "|" + fileName.Replace("|", "");

            if (objectName == "") return output;
            output += "|" + objectName.Replace("|", "");

            return output;
        }

        public static VrcfObjectId FromObject(Object obj) {
            var output = new VrcfObjectId();
            if (obj == null) return output;
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var guid, out long fileId))
                return output;
            output.guid = guid;
            var isMainAsset = AssetDatabase.IsMainAsset(obj);
            output.fileId = isMainAsset ? 0 : fileId;
            output.fileName = AssetDatabase.GetAssetPath(obj);
            output.objectName = isMainAsset ? "" : obj.name;
            return output;
        }

        [CanBeNull]
        public T ToObject<T>() where T : Object {
            if (string.IsNullOrWhiteSpace(guid)) return null;

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == null) return null;

            if (fileId == 0) {
                return AssetDatabase.LoadMainAssetAtPath(path) as T;
            }
            
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) {
                if (!(asset is T t)) continue;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var assetGuid, out long assetFileId)) continue;
                if (assetGuid != guid) continue;
                if (assetFileId != fileId) continue;
                return t;
            }
        
            // Sometimes the fileId of the main animator controller in a file changes randomly, so if the main asset
            // in the file is the right type, just assume it's the one we're looking for.
            return AssetDatabase.LoadMainAssetAtPath(path) as T;
        }

        public string Pretty() {
            if (!string.IsNullOrWhiteSpace(objectName)) {
                return $"{objectName} from {fileName}";
            }
            if (!string.IsNullOrWhiteSpace(fileName)) {
                return fileName;
            }
            if (!string.IsNullOrWhiteSpace(guid)) {
                return $"GUID {guid}";
            }
            return "Unset";
        }
    }
}
