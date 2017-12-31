using ColossalFramework.UI;
using CSUtil.Commons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TrafficManager.Manager.Impl;
using UnityEngine;

namespace TrafficManager.UI.MainMenu {
    class ExperimentalFeatureMenu : UIPanel {
        private static readonly JunctionDataExportService StepDataService = JunctionDataExportService.Instance;
        private static UILabel _title;
        private static UITextField _loadSettingsTextField;
        private static UIButton _loadSettingsButton;
        private static UIButton _clearTextAreaButton;
        private static UIButton _exportDataToConsoleButton;

        public override void Start() {
            isVisible = false;

            backgroundSprite = "GenericPanel";
            color = new Color32(75, 75, 135, 255);
            height = 40;
            width = 300;

            relativePosition = new Vector3(800f, 20f);
            _title = AddUIComponent<UILabel>();
            _title.text = "Experimental features control";
            _title.relativePosition = new Vector3(20f, 10f);

            int y = 35;

            _loadSettingsTextField = CreateTextArea("Paste settings", y);
            _loadSettingsTextField.eventTextChanged += _loadSettingsTextField_eventTextChanged;
            y += 160;
            height += 170;

            _loadSettingsButton = CreateButton("Load settings", y, ClickLoadSettings);
            y += 40;
            height += 40;
            _clearTextAreaButton = CreateButton("Clear text", y, ClickClearText);
            y += 40;
            height += 40;
            _exportDataToConsoleButton = CreateButton("Export selected TL step to console", y, ClickExportData);
            height += 25;
        }

        private void _loadSettingsTextField_eventTextChanged(UIComponent component, string value) {
            UITextField textField = component as UITextField;
            if (textField == null) return;
            if (textField.text.Length == 0) {
                _loadSettingsButton.isInteractive = false;
                _loadSettingsButton.Disable();
            } else {
                if (_loadSettingsButton.isInteractive) return;

                _loadSettingsButton.isInteractive = true;
                _loadSettingsButton.Enable();
            }
        }

        private UITextField CreateTextArea(string text, int y) {
            UITextField textfield = AddUIComponent<UITextField>();
            textfield.relativePosition = new Vector3(10f, y);
            textfield.horizontalAlignment = UIHorizontalAlignment.Left;
            textfield.text = text;
            textfield.textScale = 0.8f;
            textfield.color = Color.black;
            textfield.cursorBlinkTime = 0.45f;
            textfield.cursorWidth = 1;
            textfield.selectionBackgroundColor = new Color(35, 80, 150, 255);
            textfield.selectionSprite = "EmptySprite";
            textfield.verticalAlignment = UIVerticalAlignment.Middle;
            textfield.padding = new RectOffset(5, 0, 5, 0);
            textfield.foregroundSpriteMode = UIForegroundSpriteMode.Fill;
            textfield.normalBgSprite = "TextFieldPanel";
            textfield.hoveredBgSprite = "TextFieldPanelHovered";
            textfield.focusedBgSprite = "TextFieldPanel";
            textfield.size = new Vector3(300, 150);
            textfield.isInteractive = true;
            textfield.enabled = true;
            textfield.readOnly = false;
            textfield.builtinKeyNavigation = true;
            textfield.multiline = true;
            textfield.width = width - 20;
            return textfield;
        }

        private UIButton CreateButton(string text, int y, MouseEventHandler eventClick) {
            UIButton button = AddUIComponent<UIButton>();
            button.textScale = 0.8f;
            button.width = width - 20;
            button.height = 30;
            button.normalBgSprite = "ButtonMenu";
            button.disabledBgSprite = "ButtonMenuDisabled";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.focusedBgSprite = "ButtonMenu";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.textColor = new Color32(255, 255, 255, 255);
            button.playAudioEvents = true;
            button.text = text;
            button.relativePosition = new Vector3(10f, y);
            button.eventClick += delegate (UIComponent component, UIMouseEventParameter eventParam) {
                eventClick(component, eventParam);
                button.Invalidate();
            };

            return button;
        }

        private void ClickClearText(UIComponent component, UIMouseEventParameter eventParam) {
            _loadSettingsTextField.text = "";
            Log.Info("Load settings text area cleared");
        }

        private void ClickLoadSettings(UIComponent component, UIMouseEventParameter eventParameter) {
            string message = "Load settings clicked... \n"
                + "==========Loading Settings==========\n"
                + _loadSettingsTextField.text + "\n"
                + "========Loading Settings End========\n";
            Debug.Log(message);
        }

        private void ClickExportData(UIComponent component, UIMouseEventParameter eventParameter) {
            Debug.Log("Export log test");
            Debug.Log("=======================\n" + StepDataService.ExportJunctionSegmentsInfo() + "\n=======END=======");
        }
    }
}
