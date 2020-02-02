namespace TrafficManager.UI {
    using ColossalFramework.UI;
    using CSUtil.Commons;
    using UnityEngine;

    public class CustomKeyHandler : UICustomControl {
        // TODO add more key bindings or refactor to mod key shortcut manager
        public void OnKeyDown(UIComponent comp, UIKeyEventParameter p) {
            if (p.used || p.keycode != KeyCode.Escape) {
                return;
            }

            Log._Debug("CustomKeyHandler::OnKeyDown(KeyCode.Escape) call");
            p.Use();
        }
    }
}