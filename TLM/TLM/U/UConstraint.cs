using System;
using CSUtil.Commons;
using UnityEngine;

namespace TrafficManager.U {
    public class UConstraint {
        public float value_;
        public Unit unit_;
        public TransformField field_;

        public UConstraint(TransformField field, float value, Unit unit) {
            this.field_ = field;
            this.unit_ = unit;
            this.value_ = value;
        }

//        private float GetValue(TransformField field, RectTransform rt) {
//            switch (field) {
//                case TransformField.Left: return rt.localPosition.x;
//                case TransformField.Top: return rt.localPosition.y;
//                case TransformField.Right: return rt.localPosition.x + rt.sizeDelta.x;
//                case TransformField.Bottom: return rt.localPosition.y + rt.sizeDelta.y;
//                case TransformField.Width: return rt.sizeDelta.x;
//                case TransformField.Height: return rt.sizeDelta.y;
//                default: 
//                    throw new ArgumentOutOfRangeException(nameof(field), field, null);
//            }
//        }

        private void SetValue(TransformField field, RectTransform rt, float value) {
            switch (field) {
                case TransformField.Left: {
                    // Left: rectTransform.offsetMin.x;
                    Vector3 pos = rt.offsetMin;
                    pos.x = value;
                    rt.offsetMin = pos;
                    break;
                }
                case TransformField.Top: {
                    // Top: rectTransform.offsetMax.y
                    Vector3 pos = rt.offsetMax;
                    pos.y = GetValue(Unit.ParentHeight, rt) - value;
//                    pos.y = value;
                    rt.offsetMax = pos;
                    break;
                } 
                case TransformField.Right: {
                    // Right: rectTransform.offsetMax.x
                    Vector3 pos = rt.offsetMax;
                    pos.x = value;
                    rt.offsetMax = pos;
                    break;
                }
                case TransformField.Bottom: {
                    // Bottom: rectTransform.offsetMin.y
                    Vector3 pos = rt.offsetMin;
                    pos.y = GetValue(Unit.ParentHeight, rt) - value;
//                    pos.y = value;
                    rt.offsetMin = pos;
                    break;
                }
                case TransformField.Width: {
                    Vector3 size = rt.sizeDelta;
                    size.x = value;
                    rt.sizeDelta = size;
                    break;
                }
                case TransformField.Height: {
                    Vector3 size = rt.sizeDelta;
                    size.y = value;
                    rt.sizeDelta = size;
                    break;
                }
                default: throw new ArgumentOutOfRangeException(nameof(field), field, null);
            }
        }
        
        public void Apply(RectTransform rt) {
            float val = GetValue(this.unit_, rt);
            Log._Debug($"Apply {this.field_} {this.value_} based on {val}");
            SetValue(this.field_, rt, this.value_ * val);
            Log._Debug($"New recttransform {Log.RectTransform(rt)}");
        }

        private static float GetValue(Unit unit, RectTransform rt) {
            switch (unit) {
                case Unit.ScreenWidth: return Screen.width;
                case Unit.ScreenHeight: return Screen.height;
                case Unit.OwnWidth: return rt.sizeDelta.x;
                case Unit.OwnHeight: return rt.sizeDelta.y;
                case Unit.ParentWidth: {
                    var parentRt = rt.parent as RectTransform;
                    return parentRt != null ? parentRt.sizeDelta.x : Screen.width;
                }
                case Unit.ParentHeight: {
                    var parentRt = rt.parent as RectTransform;
                    return parentRt != null ? parentRt.sizeDelta.y : Screen.height;
                }
                case Unit.Pixels: return 1f;
                default: throw new ArgumentOutOfRangeException();
            }
        }
    }
}