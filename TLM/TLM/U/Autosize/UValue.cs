namespace TrafficManager.U.Autosize {
    using System;
    using ColossalFramework.UI;
    using UnityEngine;

    /// <summary>
    /// A value which can be calculated based on control, parent control, and other global data.
    /// Used for smart control sizes and positioning.
    /// </summary>
    public struct UValue {
        public USizeRule Rule;
        public float Value;

        // public UValue() {
        //     Rule = USizeRule.Ignore;
        //     Value = 0f;
        // }

        public UValue(USizeRule r, float v) {
            Rule = r;
            Value = v;
        }

        /// <summary>Calculates value based on the UI component.</summary>
        /// <param name="self">The UI component.</param>
        /// <returns>The calculated value.</returns>
        public float Calculate(UIComponent self) {
            switch (Rule) {
                case USizeRule.Ignore:
                    return 0f;
                case USizeRule.FixedSize:
                    return Value;
                case USizeRule.FractionScreenWidth:
                    return Screen.width * Value;
                case USizeRule.MultipleOfWidth:
                    return self.width * Value;
                case USizeRule.FractionScreenHeight:
                    return Screen.height * Value;
                case USizeRule.MultipleOfHeight:
                    return self.height * Value;
                case USizeRule.ReferenceWidthAt1080P:
                    return (Screen.width * Value) / 1920f;
                case USizeRule.ReferenceHeightAt1080P:
                    return (Screen.height * Value) / 1080f;
                case USizeRule.FitChildrenWidth:
                case USizeRule.FitChildrenHeight:
                    // For this rule to work, bounding box must be known
                    // Handle this in USizePosition.UpdateControl
                    throw new Exception("Rule is not supported in Calculate, handle this outside");
            }
            throw new ArgumentOutOfRangeException();
        }

        public static UValue ReferenceWidthAt1080P(float f) {
            return new UValue(USizeRule.ReferenceWidthAt1080P, f);
        }

        /// <summary>Returns multiple of current control width times f.</summary>
        /// <param name="f">The multiplier.</param>
        /// <returns>Value.</returns>
        public static UValue MultipleOfWidth(float f) {
            return new UValue(USizeRule.MultipleOfWidth, f);
        }

        /// <summary>Returns multiple of current control height times f.</summary>
        /// <param name="f">The multiplier.</param>
        /// <returns>Value.</returns>
        public static UValue MultipleOfHeight(float f) {
            return new UValue(USizeRule.MultipleOfHeight, f);
        }

        /// <summary>Adjusts width to fit all children.</summary>
        /// <param name="f">Padding.</param>
        /// <returns>New UValue.</returns>
        public static UValue FitChildrenWidth(float f) {
            return new UValue(USizeRule.FitChildrenWidth, f);
        }

        /// <summary>Adjusts height to fit all children.</summary>
        /// <param name="f">Padding.</param>
        /// <returns>New UValue.</returns>
        public static UValue FitChildrenHeight(float f) {
            return new UValue(USizeRule.FitChildrenHeight, f);
        }
    }
}