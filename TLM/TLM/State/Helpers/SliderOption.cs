namespace TrafficManager.State.Helpers; 
using ICities;
using ColossalFramework.UI;
using CSUtil.Commons;
using UnityEngine;

public class SliderOption : UIOptionBase<float> {

    private const int SLIDER_LABEL_MAX_WIDTH = 695;

    private byte _step = 5;
    private UILabel _sliderLabel;
    private UISlider _ui;
    public bool HasUI => _ui != null;

    public byte Min => (Option as FloatOption).Min;
    public byte Max => (Option as FloatOption).Max;
    public byte Step {
        get => _step;
        set {
            if (_step == value) return;

            _step = value;
            if (HasUI) _ui.stepSize = value;
        }
    }

    public override void SetValue(float value) {
        if (Mathf.Approximately(value, _ui.value)) return;
        Log.Info($"SliderOption.Value: `Name` changed to {value}");
        if (HasUI) {
            _ui.value = value;
            UpdateTooltip();
        }
    }

    public virtual SliderOption AddUI(UIHelperBase container) {
        _ui = container.AddSlider(
            text: Translate(Label) + ":",
            min: Min,
            max: Max,
            step: Step,
            defaultValue: Option.Value,
            eventCallback: OnValueChanged) as UISlider;
        _sliderLabel = _ui.parent.Find<UILabel>("Label");
        _sliderLabel.width = SLIDER_LABEL_MAX_WIDTH;
        InitUI(_ui);
        return this;
    }

    protected override void UpdateLabel() {
        if (_ui == null) return;

        string tooltip = IsInScope ? $"{Option.Value}{_tooltip}" : Translate(INGAME_ONLY_SETTING);
        string label = Translate(Label);
        _sliderLabel.text = label + ": " + tooltip;
    }

    protected override void UpdateTooltip() => UpdateLabel();

    protected override void UpdateReadOnly() {
        if (_ui == null) return;

        var readOnly = !IsInScope || _readOnly;

        Log._Debug($"SliderOption.UpdateReadOnly() - `Name` is {(readOnly ? "read-only" : "writeable")}");

        _ui.isInteractive = !readOnly;
        _ui.thumbObject.isInteractive = !readOnly;
        _ui.thumbObject.opacity = readOnly ? 0.3f : 1f;
        // parent is UIPanel containing text label and slider
        _sliderLabel.opacity = readOnly ? 0.3f : 1f;
    }
}