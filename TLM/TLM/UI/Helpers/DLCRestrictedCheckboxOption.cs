namespace TrafficManager.UI.Helpers;

using System;
using ColossalFramework.Globalization;
using ColossalFramework.UI;
using ICities;
using TrafficManager.State;
using TrafficManager.Util.Extensions;
using UnityEngine;

public class DLCRestrictedCheckboxOption : CheckboxOption {
    private SteamHelper.DLC _requiredDLC;

    public DLCRestrictedCheckboxOption(string fieldName,
                                       SteamHelper.DLC requiredDLC,
                                       Scope scope = Scope.Savegame) : base(fieldName, scope) {
        _requiredDLC = requiredDLC;
        _readOnly = !SteamHelper.IsDLCOwned(_requiredDLC);
    }

    public override CheckboxOption AddUI(UIHelperBase container) {
        UIPanel panel = container.AddUIComponent<UIPanel>();
        panel.autoLayout = false;
        panel.size = new Vector2(720, 22);//default checkbox template size
        panel.relativePosition = Vector3.zero;
        UIPanel innerPanel = panel.AddUIComponent<UIPanel>();
        UISprite icon = innerPanel.AddUIComponent<UISprite>();
        icon.relativePosition = Vector3.zero;
        icon.size = new Vector2(24, 24);
        icon.spriteName = MapToSpriteIconName(_requiredDLC);

        var option = base.AddUI(new UIHelper(innerPanel));
        innerPanel.relativePosition = new Vector3(-icon.size.x, 0);
        innerPanel.autoLayoutDirection = LayoutDirection.Horizontal;
        innerPanel.autoFitChildrenHorizontally = true;
        innerPanel.autoFitChildrenVertically = true;
        innerPanel.autoLayout = true;

        if (_readOnly) {
            icon.tooltip = Locale.Get("CONTENT_REQUIRED", _requiredDLC.ToString());
            _ui.tooltip = Translate("Checkbox:DLC is required to change this option and see effects in game");
        }

        return option;
    }

    private static string MapToSpriteIconName(SteamHelper.DLC dlc) {
        switch (dlc) {
            case SteamHelper.DLC.AfterDarkDLC:
                return "ADIcon";
            case SteamHelper.DLC.NaturalDisastersDLC:
                return "NaturalDisastersIcon";
            case SteamHelper.DLC.SnowFallDLC:
                return "WWIcon";
            default:
                return string.Empty;
        }
    }

}