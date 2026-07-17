using UnityEngine;
using UnityEngine.UIElements;
using VF.Utils;

namespace VF.Inspector {
    internal class TextFieldWithPlaceholder : TextField {
        private readonly Label placeholderLabel;
        private string placeholder;

        public string Placeholder {
            get => placeholder;
            set {
                placeholder = value;
                placeholderLabel.text = value ?? "";
                RefreshPlaceholder();
            }
        }

        public TextFieldWithPlaceholder() {
            placeholderLabel = new Label("").TextWrap();
            placeholderLabel.pickingMode = PickingMode.Ignore;
            placeholderLabel.style.position = Position.Absolute;
            placeholderLabel.style.left = 4;
            placeholderLabel.style.right = 4;
            placeholderLabel.style.top = 0;
            placeholderLabel.style.bottom = 0;
            placeholderLabel.style.color = new Color(1, 1, 1, 0.4f);
            placeholderLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            Add(placeholderLabel);

            RegisterCallback<ChangeEvent<string>>(_ => RefreshPlaceholder());
            RegisterCallback<AttachToPanelEvent>(_ => {
                schedule.Execute(RefreshPlaceholder);
            });
        }

        private void RefreshPlaceholder() {
            placeholderLabel.style.display = string.IsNullOrWhiteSpace(value)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
}
