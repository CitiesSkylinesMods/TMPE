namespace TrafficManager.UI.Helpers {
    using ColossalFramework.UI;
    using System;
    using TrafficManager.U;
    using TrafficManager.Util;
    using UnityEngine;

    public class UITriStateCheckbox : UICheckBox {
        public event PropertyChangedEventHandler<bool?> EventValueChanged;

        public UIComponent FalseComponent;
        public UIComponent NullComponent;
        public UIComponent TrueComponent {
            get => checkedBoxObject;
            set => checkedBoxObject = value;
        }

        private bool? value_;
        public bool? Value {
            get => value_;
            set {
                if (value != value_) {
                    value_ = value;
                    OnValueChanged();
                }
            }
        }

        [Obsolete("use Value instead", error: true)]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:Element should begin with upper-case letter", Justification = "N/A")]
        public new bool isChecked { get; set; }

        protected virtual void OnValueChanged() {
            RefreshIcon();
            EventValueChanged?.Invoke(this, Value);
        }

        private void RefreshIcon() {
            if (TrueComponent) TrueComponent.isVisible = Value == true;
            if (FalseComponent) FalseComponent.isVisible = Value == false;
            if (NullComponent) NullComponent.isVisible = Value == null;
            RefreshTooltip();
        }

        public override void Awake() {
            base.Awake();
            name = GetType().Name;
            size = new Vector2(716, 22);
            IntVector2 spriteSize = new(39, 22);
            AtlasBuilder atlasBuilder = new AtlasBuilder(name, "Options", spriteSize);
            string[] names = new[] { "tristate-false", "tristate-null", "tristate-true" };
            foreach(string name in names)
                atlasBuilder.Add(new AtlasSpriteDef(name, spriteSize));
            var atlas = atlasBuilder.CreateAtlas();
            disabledColor = new Color32(71, 71, 71, 255);

            UISprite sprite = AddUIComponent<UISprite>();
            sprite.atlas = atlas;
            sprite.spriteName = names[0];
            sprite.tooltip = "no";
            sprite.size = new(39, 22);
            sprite.relativePosition = new Vector2(0, Mathf.FloorToInt((height - sprite.height) / 2));
            sprite.disabledColor = disabledColor;
            FalseComponent = sprite;

            var sprite2 = AddUIComponent<UISprite>();
            sprite2.atlas = atlas;
            sprite2.spriteName = names[1];
            sprite2.tooltip = "N/A";
            sprite2.size = sprite.size;
            sprite2.relativePosition = sprite.relativePosition;
            sprite2.disabledColor = disabledColor;
            NullComponent = sprite2;

            var sprite3 = AddUIComponent<UISprite>();
            sprite3.atlas = atlas;
            sprite3.spriteName = names[2];
            sprite3.tooltip = "yes";
            sprite3.size = sprite.size;
            sprite3.relativePosition = sprite.relativePosition;
            sprite3.disabledColor = disabledColor;
            TrueComponent = sprite3;

            label = AddUIComponent<UILabel>();
            label.text = name;
            label.textScale = 1.125f;
            label.disabledColor = label.disabledTextColor = disabledColor;
            label.relativePosition = new Vector2(
                sprite.relativePosition.x + sprite.width + 5f,
                Mathf.FloorToInt((height - label.height) / 2));

        }

        public override void Start() {
            base.Start();
            RefreshIcon();
        }

        protected override void OnClick(UIMouseEventParameter p) {
            if (!readOnly && !p.used) {
                // null -> true -> false
                Value = Value switch {
                    true => false,
                    false=> null,
                    null => true,
                };
                p.Use();
            }

            base.OnClick(p);
        }
    }
}
