namespace TrafficManager.UI.MainMenu {
    using System.Collections.Generic;
    using System.Linq;
    using ColossalFramework.UI;
    using TrafficManager.U;
    using TrafficManager.U.Autosize;
    using UnityEngine;
    using Debug = System.Diagnostics.Debug;

    public partial class MainMenuWindow {
        internal class ToolPanel : UPanel {
            internal struct AddButtonsResult {
                public List<BaseMenuButton> Buttons;
                public MainMenuLayout Layout;
            }

            /// <summary>Create buttons and add them to the given panel UIBuilder.</summary>
            /// <param name="window">The parent window.</param>
            /// <param name="parentComponent">The parent panel component to host the buttons.</param>
            /// <param name="builder">UI builder to use.</param>
            /// <param name="buttonDefs">The button definitions array.</param>
            /// <param name="minRowLength">Shortest horizontal row length allowed before breaking new row.</param>
            /// <returns>A list of created buttons.</returns>
            private AddButtonsResult AddButtonsFromButtonDefinitions(
                MainMenuWindow window,
                UIComponent parentComponent,
                UBuilder builder,
                MenuButtonDef[] buttonDefs,
                int minRowLength) {
                AddButtonsResult result;
                result.Buttons = new List<BaseMenuButton>();

                // Count the button objects and set their layout
                result.Layout = new MainMenuLayout();
                result.Layout.CountEnabledButtons(buttonDefs);
                int placedInARow = 0;

                foreach (MenuButtonDef buttonDef in buttonDefs) {
                    if (!buttonDef.IsEnabledFunc()) {
                        // Skip buttons which are not enabled
                        continue;
                    }

                    // Create and populate the panel with buttons
                    var button = parentComponent.AddUIComponent(buttonDef.ButtonType) as BaseMenuButton;

                    // Count buttons in a row and break the line
                    bool doRowBreak = result.Layout.IsRowBreak(placedInARow, minRowLength);

                    button.ResizeFunction(
                        resizeFn: (UResizer r) => {
                            r.Stack(doRowBreak ? UStackMode.NewRowBelow : UStackMode.ToTheRight);
                            r.Width(UValue.FixedSize(40f));
                            r.Height(UValue.FixedSize(40f));
                        });

                    if (doRowBreak) {
                        placedInARow = 0;
                        result.Layout.Rows++;
                    } else {
                        placedInARow++;
                    }

                    // Also ask each button what sprites they need
                    button.SetupButtonSkin(builder.AtlasBuilder);

                    // Take button classname, split by ".", and the last word becomes the button name
                    string buttonName = buttonDef.ButtonType.ToString().Split('.').Last();
                    button.name = $"TMPE_MainMenuButton_{buttonName}";

                    window.ButtonsDict.Add(buttonDef.Mode, button);
                    result.Buttons.Add(button);
                }

                return result;
            }

            public AddButtonsResult SetupToolButtons(MainMenuWindow window, UBuilder builder) {
                this.name = "TMPE_MainMenu_ToolPanel";
                this.ResizeFunction(
                    (UResizer r) => {
                        r.Stack(mode: UStackMode.Below);
                        r.FitToChildren();
                    });

                // Create 1 or 2 rows of button objects
                var toolButtonsResult = AddButtonsFromButtonDefinitions(
                    window,
                    parentComponent: this,
                    builder,
                    buttonDefs: TOOL_BUTTON_DEFS,
                    minRowLength: 4);
                window.ToolButtonsList = toolButtonsResult.Buttons;

                return toolButtonsResult;
            }

            public void SetupExtraButtons(MainMenuWindow window,
                                          UBuilder builder,
                                          AddButtonsResult toolButtonsResult) {
                this.name = "TMPE_MainMenu_ExtraPanel";

                // Silver background panel
                this.atlas = TextureUtil.Ingame;
                this.backgroundSprite = "GenericPanel";

                // The panel will be Dark Silver at 50% dark 100% alpha
                this.color = new Color32(128, 128, 128, 255);

                this.ResizeFunction(
                    resizeFn: (UResizer r) => {
                        // Step to the right by 4px
                        r.Stack(
                            mode: UStackMode.ToTheRight,
                            spacing: UConst.UIPADDING);
                        r.FitToChildren();
                    });

                // Place two extra buttons (despawn & clear traffic).
                // Use as many rows as in the other panel.
                var extraButtonsResult = AddButtonsFromButtonDefinitions(
                    window,
                    parentComponent: this,
                    builder,
                    buttonDefs: EXTRA_BUTTON_DEFS,
                    minRowLength: toolButtonsResult.Layout.Rows == 2 ? 1 : 2);
                window.ExtraButtonsList = extraButtonsResult.Buttons;
            }
        }
    }
}