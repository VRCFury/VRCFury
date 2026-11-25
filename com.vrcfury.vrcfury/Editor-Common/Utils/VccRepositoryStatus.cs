using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace VF.Utils {
    internal static class VccRepositoryStatus {
        private const string RepoId = "com.vrcfury.vcc";
        private const string RepoUrl = "https://vcc.vrcfury.com";
        private static Task<string> warningCheck;
        private static DateTime nextCheckUtc;
        private static (string selectionPath, (DateTime, long) selectionStamp, (DateTime, long) repositoryList) cachedSettingsStamp;
        private static string cachedWarning;

        // Share the background read across inspectors opened together.
        public static Task<string> GetWarning() {
            if (Application.isBatchMode) return Task.FromResult<string>(null);
            if (warningCheck?.Status == TaskStatus.RanToCompletion && warningCheck.Result == null) return warningCheck;
            if (warningCheck == null || (warningCheck.IsCompleted && DateTime.UtcNow >= nextCheckUtc)) {
                nextCheckUtc = DateTime.UtcNow.AddSeconds(5);
                var windows = Application.platform == RuntimePlatform.WindowsEditor;
                warningCheck = Task.Run(() => ReadWarning(GetDataHome(windows), windows));
            }
            return warningCheck;
        }

        private static string GetDataHome(bool windows) {
            if (windows) return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDataDirectory = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrEmpty(appDataDirectory)) return appDataDirectory;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrEmpty(home) ? null : Path.Combine(home, ".local", "share");
        }

        internal static string ReadWarning(string appDataDirectory, bool windows = true) {
            if (string.IsNullOrEmpty(appDataDirectory)) return null;
            var vccDataDirectory = Path.Combine(appDataDirectory, "VRChatCreatorCompanion");
            var repositoryListPath = Path.Combine(vccDataDirectory, "settings.json");
            var alcomSettingsPath = Path.Combine(vccDataDirectory, "vrc-get", "gui-config.json");
            var vccExtendedSettingsPath = Path.Combine(vccDataDirectory, "settings_extended.json");
            var webViewSelection = windows ? LevelDbValueReader.GetRepositorySelection(appDataDirectory) : default;
            var (selectionPath, selectionStamp) = new[] {
                (path: alcomSettingsPath, stamp: FileStamp(alcomSettingsPath)),
                (path: vccExtendedSettingsPath, stamp: windows ? FileStamp(vccExtendedSettingsPath) : default),
                (path: webViewSelection.directory, stamp: (modified: webViewSelection.modified, length: 0L))
            }.OrderByDescending(candidate => candidate.stamp.modified).First();
            if (selectionStamp.modified == default) return null;
            var settingsStamp = (selectionPath, selectionStamp, FileStamp(repositoryListPath));
            if (settingsStamp == cachedSettingsStamp) return cachedWarning;

            try {
                var repositoryList = ReadSettings(repositoryListPath);
                if (selectionPath == webViewSelection.directory) {
                    cachedWarning = CheckRepository("VCC", repositoryList, repo => IsListed(webViewSelection.readSelection(), repo) == false);
                } else {
                    var selectionSettings = ReadSettings(selectionPath);
                    cachedWarning = selectionPath == alcomSettingsPath
                        ? CheckRepository("ALCOM", repositoryList, repo => IsListed(selectionSettings["guiHiddenRepositories"], repo) == true)
                        : CheckRepository("VCC", repositoryList, repo => IsListed(selectionSettings["enabledRepos"], repo) == false
                            || IsListed(selectionSettings["disabledRepos"], repo) == true);
                }
                cachedSettingsStamp = settingsStamp;
                return cachedWarning;
            } catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException) {
                return null;
            }
        }

        private static (DateTime modified, long length) FileStamp(string path) {
            var info = new FileInfo(path);
            return info.Exists ? (info.LastWriteTimeUtc, info.Length) : default;
        }

        private static string CheckRepository(string app, JObject settings, Func<JObject, bool> isDisabled) {
            var repositoryList = settings?["userRepos"];
            if (settings == null || (repositoryList != null && repositoryList.Type != JTokenType.Null && !(repositoryList is JArray))) return null;
            var repo = (repositoryList as JArray)?.OfType<JObject>().FirstOrDefault(entry =>
                ReadString(entry["id"]) == RepoId || IsOfficialUrl(ReadString(entry["url"])));
            var issue = repo == null ? "missing" : isDisabled(repo) ? "disabled" : null;
            return issue == null ? null : $"VRCFury is {issue} in your {app} repository settings. "
                + "You will not receive updates until this is fixed. The repository URL is vcc.vrcfury.com";
        }

        private static JObject ReadSettings(string path) {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return JObject.Parse(reader.ReadToEnd());
        }

        private static string ReadString(JToken token) => (token as JValue)?.Value as string;
        private static bool IsOfficialUrl(string url) => url?.TrimEnd('/') == RepoUrl;
        private static bool? IsListed(JToken list, JObject repo) => (list as JArray)?.Select(ReadString)
            .Any(name => name != null && (name == RepoId || name == ReadString(repo["id"]) || IsOfficialUrl(name)));
    }
}
