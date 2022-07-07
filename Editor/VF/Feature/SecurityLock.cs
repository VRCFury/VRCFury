using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace VF.Feature {

public class SecurityLock : BaseFeature<VF.Model.Feature.SecurityLock> {
    public override void Apply() {
        if (model.leftCode == 0 || model.rightCode == 0) return;

        var paramSecuritySync = manager.NewBool("SecurityLockSync", synced: true, defTrueInEditor: true);
        // This doesn't actually need synced, but vrc gets annoyed that the menu is using an unsynced param
        var paramSecurityMenu = manager.NewBool("SecurityLockMenu", synced: true);
        manager.NewMenuToggle("Security", paramSecurityMenu);
        var layer = manager.NewLayer("Security Lock");
        var entry = layer.NewState("Entry");
        var remote = layer.NewState("Remote").Move(entry, 0, -1);
        var locked = layer.NewState("Locked").Move(entry, 0, 1);
        var check = layer.NewState("Check");
        var unlocked = layer.NewState("Unlocked").Move(check, 1, 0);

        entry.TransitionsTo(remote).When(IsLocal().IsFalse());
        entry.TransitionsTo(locked).When(Always());

        locked.Drives(paramSecurityMenu, false);
        locked.Drives(paramSecuritySync, false);
        locked.TransitionsTo(check).When(paramSecurityMenu.IsTrue());

        check.TransitionsTo(unlocked).When(GestureLeft().IsEqualTo(model.leftCode).And(GestureRight().IsEqualTo(model.rightCode)));
        check.TransitionsTo(locked).When(Always());

        unlocked.Drives(paramSecuritySync, true);
        unlocked.TransitionsTo(locked).When(paramSecurityMenu.IsFalse());
    }

    public override string GetEditorTitle() {
        return "Security Lock";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();
        content.Add(new PropertyField(prop.FindPropertyRelative("leftCode"), "Left Hand Code"));
        content.Add(new PropertyField(prop.FindPropertyRelative("rightCode"), "Right Hand Code"));
        return content;
    }
    
    public override bool AvailableOnProps() {
        return false;
    }
}

}
