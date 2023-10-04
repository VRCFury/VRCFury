using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace VF.Inspector {
    public class VRCFurySearchWindowProvider : ScriptableObject, ISearchWindowProvider
    {
        private Func<List<SearchTreeEntry>> _onCreateSearchTree;
        private List<string> _staticSearchEntries;
        private Func<SearchTreeEntry, object, bool> _onSelectEntry;
        private object _userData;
        
        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context) {
            if (_staticSearchEntries == null) {
                return _onCreateSearchTree?.Invoke();
            }
            
            var entries = new List<SearchTreeEntry>();
            foreach (var entry in _staticSearchEntries) {
                entries.Add(new SearchTreeEntry(new GUIContent(entry)));
            }
            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            return _onSelectEntry?.Invoke(entry, _userData) ?? false;
        }
        
        public void InitProvider(Func<List<SearchTreeEntry>> onCreateSearchTree, Func<SearchTreeEntry, object, bool> onSelectEntry, object userData = null) {
            _onCreateSearchTree = onCreateSearchTree;
            _onSelectEntry = onSelectEntry;
            _userData = userData;
        }
        
        public void InitProvider(List<string> staticSearchEntries, Func<SearchTreeEntry, object, bool> onSelectEntry, object userData = null) {
            _staticSearchEntries = staticSearchEntries;
            _onSelectEntry = onSelectEntry;
            _userData = userData;
        }
    }
}