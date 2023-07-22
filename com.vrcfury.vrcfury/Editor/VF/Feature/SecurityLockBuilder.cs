using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {

public class SecurityLockBuilder : FeatureBuilder<SecurityLock> {
    private VFABool _unlockedParam = null;
    private VFABool GetUnlockedParam() {
        if (_unlockedParam == null) _unlockedParam = GetFx().NewBool("SecurityLockSync", synced: true);
        return _unlockedParam;
    }
    public VFACondition GetEnabled() {
        return GetUnlockedParam().IsTrue();
    }
    
    [FeatureBuilderAction]
    public void Apply() {
        if (allFeaturesInRun.Where(m => m is SecurityLock).Count() > 1) {
            throw new VRCFBuilderException("Cannot have more than one SecurityLock feature on avatar.");
        }
        
        var unlockCode = model.pinNumber;
        var digits = unlockCode
            .Select(c => c - '0')
            .ToArray();
        if (digits.Length < 4) {
            throw new VRCFBuilderException("Security lock must contain at least 4 digits");
        }
        if (digits.Length > 10) {
            throw new VRCFBuilderException("Security lock must contain at most 10 digits");
        }
        foreach (var digit in digits) {
            if (digit < 1 || digit > 7) {
                throw new VRCFBuilderException("Security lock contains digit outside allowed bounds (1-7)");
            }
        }
        var numDigits = digits.Length;
        var numDigitSlots = 10;

        var fx = GetFx();
        var paramSecuritySync = GetUnlockedParam();
        // This doesn't actually need synced, but vrc gets annoyed that the menu is using an unsynced param
        var paramInput = fx.NewInt("SecurityInput", synced: true, networkSynced: false);
        for (var i = 1; i < 8; i++) {
            manager.GetMenu().NewMenuToggle("Security/" + i, paramInput, i);
        }
        manager.GetMenu().NewMenuToggle("Security/Unlocked", paramInput, 8);
        var layer = fx.NewLayer("Security Lock");
        
        var remote = layer.NewState("Remote Trap");

        var entry = layer.NewState("Entry")
            .Move(remote, 1, 0);
        
        var clear = layer.NewState("Clear")
            .Move(entry, 1, 1);
        clear.TransitionsTo(entry).When(fx.Always());

        remote.TransitionsTo(entry).When(fx.IsLocal().IsTrue());
        
        var digitParams = new List<VFAInteger>();
        for (var i = 0; i < numDigitSlots; i++) {
            var savedDigit = fx.NewInt("SecurityDigit" + i);
            digitParams.Add(savedDigit);
            clear.Drives(savedDigit, 0);
        }

        var saveStates = new List<VFAState>();
        for (var i = numDigitSlots - 1; i >= 0; i--) {
            var saveState = layer.NewState("Save " + i);
            if (saveStates.Count == 0) saveState.Move(entry, -1, 1);
            saveStates.Add(saveState);
            var target = digitParams[i];
            var source = i == 0 ? paramInput : digitParams[i - 1];
            saveState.DrivesCopy(target, source);
            if (saveStates.Count > 1) {
                saveStates[saveStates.Count - 2].TransitionsTo(saveState).When(fx.Always());
            } else {
                entry.TransitionsTo(saveState).When(paramInput.IsGreaterThan(0).And(paramInput.IsLessThan(8)));
            }
        }

        entry.TransitionsTo(clear).When(paramInput.IsEqualTo(8));
        entry.Drives(paramInput, 0);
        entry.Drives(paramSecuritySync, false);

        var check = layer.NewState("Check").Move(1, -1);
        saveStates[saveStates.Count - 1].TransitionsTo(check).When(fx.Always());

        var unlocked = layer.NewState("Unlocked").Move(1,-1);
        var digitsReversed = digits.Reverse().ToArray();
        var unlockCondition = digitParams[0].IsEqualTo(digitsReversed[0]);
        for (int i = 1; i < numDigits; i++) {
            unlockCondition = unlockCondition.And(digitParams[i].IsEqualTo(digitsReversed[i]));
        }
        check.TransitionsTo(unlocked).When(unlockCondition);
        check.TransitionsTo(entry).When(fx.Always());
        unlocked.Drives(paramInput, 8);
        unlocked.Drives(paramSecuritySync, true);
        unlocked.TransitionsTo(clear).When(paramInput.IsNotEqualTo(8));
    }

    public override string GetEditorTitle() {
        return "Security Lock";
    }

    public override VisualElement CreateEditor(SerializedProperty prop) {
        var content = new VisualElement();
        content.Add(VRCFuryEditorUtils.Info("This feature will enable the security submenu in your avatar's menu. You must enter the correct pin to unlock any VRCFury toggles marked with the 'Security' flag."));
        content.Add(VRCFuryEditorUtils.WrappedLabel("Pin Number (min 4 digits, max 10 digits, can only use numbers 1-7)"));
        content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("pinNumber")));
        return content;
    }
    
    public override bool AvailableOnProps() {
        return false;
    }
}

}
