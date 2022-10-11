namespace TrafficManager.UI.Helpers {
    using ColossalFramework.UI;
    using KianCommons.UI;
    using System;
    using TrafficManager.U;
    using TrafficManager.Util;
    using UnityEngine;

    public class UITriStateCheckbox : UICheckBox {
        public event PropertyChangedEventHandler<bool?> eventValueChanged;

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
        public new bool ?isChecked { get; set; }

        protected virtual void OnValueChanged() {
            RefreshIcon();
            eventValueChanged?.Invoke(this, Value);
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
            IntVector2 spriteSize = new(51, 19);
            AtlasBuilder atlasBuilder = new AtlasBuilder(name, "Options", spriteSize);
            string[] names = new[] { "tristate-false", "tristate-null", "tristate-true" };
            foreach(string name in names)
                atlasBuilder.Add(new AtlasSpriteDef(name, spriteSize));
            var atlas = atlasBuilder.CreateAtlas();

            UISprite sprite = AddUIComponent<UISprite>();
            sprite.atlas = atlas;
            sprite.spriteName = names[0];
            sprite.tooltip = "no";
            sprite.size = new(51, 19); 
            sprite.relativePosition = new Vector2(0, Mathf.FloorToInt((height - sprite.height) / 2));
            FalseComponent = sprite;

            var sprite2 = AddUIComponent<UISprite>();
            sprite2.atlas = atlas;
            sprite2.spriteName = names[1];
            sprite2.tooltip = "N/A";
            sprite2.size = sprite.size;
            sprite2.relativePosition = sprite.relativePosition;
            NullComponent = sprite2;

            var sprite3 = AddUIComponent<UISprite>();
            sprite3.atlas = atlas;
            sprite3.spriteName = names[2];
            sprite3.tooltip = "yes";
            sprite3.size = sprite.size;
            sprite3.relativePosition = sprite.relativePosition;
            TrueComponent = sprite3;

            label = AddUIComponent<UILabel>();
            label.text = name;
            label.textScale = 1.125f;
            label.relativePosition = new Vector2(sprite.width + 5f, Mathf.FloorToInt((height - label.height) / 2));
        }

        public override void Start() {
            base.Start();
            RefreshIcon();
        }

        protected override void OnClick(UIMouseEventParameter p) {
            if (!readOnly && !p.used) {
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
