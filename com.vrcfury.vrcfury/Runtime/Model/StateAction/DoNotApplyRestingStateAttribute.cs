using System;

namespace VF.Model.StateAction {
    /**
     * Some Actions contain an implicit "resting state" which is applied to the avatar during the upload automatically.
     * For instance, if you have a Turn On action somewhere, the object will automatically be "turned off" during the upload.
     * However, if the action is annotated with this attribute, this behaviour will be skipped.
     */
    [AttributeUsage(AttributeTargets.Field)]
    internal class DoNotApplyRestingStateAttribute : Attribute {
    }
}