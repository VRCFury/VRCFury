using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace VF.Inspector {
    internal class VrcfSearchWindow {
        private readonly List<SearchTreeEntry> entries = new List<SearchTreeEntry>();

        public VrcfSearchWindow(string title) {
            entries.Add(new SearchTreeGroupEntry(new GUIContent(title), 0));
        }

        public void Open(Action<string> onSelect, Vector2? pos = null) {
            var searchContext = new SearchWindowContext(GUIUtility.GUIToScreenPoint(pos ?? Event.current.mousePosition), 500, 300);
            var provider = ScriptableObject.CreateInstance<VRCFurySearchWindowProvider>();
            provider.InitProvider(() => entries, (entry, userData) => {
                if (entry.userData != null) {
                    onSelect((string)entry.userData);
                }
                return true;
            });
            SearchWindow.Open(searchContext, provider);
        }

        public Group GetMainGroup() {
            return new Group(entries, 1);
        }

        public class Group {
            private readonly List<SearchTreeEntry> entries;
            private readonly int level;

            public Group(List<SearchTreeEntry> entries, int level) {
                this.entries = entries;
                this.level = level;
            }

            public Group AddGroup(string title) {
                entries.Add(new SearchTreeGroupEntry(new GUIContent(title), level));
                return new Group(entries, level + 1);
            }
            public void Add(string title, string value = null) {
                entries.Add(new SearchTreeEntry(new GUIContent(title)) {
                    userData = value,
                    level = level
                });
            }
        }
    }
}
