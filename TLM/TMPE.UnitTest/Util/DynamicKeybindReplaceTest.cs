namespace TMUnitTest.Util {
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using TrafficManager.U;

    [TestClass]
    public class DynamicKeybindReplaceTest {

        [TestMethod]
        public void Should_DynamicallyReplaceAndColorize_SingleValue_Having_OneReplacement() {
            string testString = "[[Page Down]] To switch underground view";
            string[] replacements = new[] { "Key_X" };
            string expected =
                $"{UConst.GetKeyboardShortcutColorTagOpener()}Key_X{UConst.KEYBOARD_SHORTCUT_CLOSING_TAG} To switch underground view";
            string result = UIUtil.ColorizeDynamicKeybinds(testString, replacements);
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void Should_DynamicallyReplaceAndColorize_TwoValues_Having_TwoReplacements() {
            string testString = "[[Page Up]] | [[Page Down]] To switch underground view";
            string[] replacements = new[] { "Key_X", "Key_Y" };
            string expected =
                $"{UConst.GetKeyboardShortcutColorTagOpener()}Key_X{UConst.KEYBOARD_SHORTCUT_CLOSING_TAG} | {UConst.GetKeyboardShortcutColorTagOpener()}Key_Y{UConst.KEYBOARD_SHORTCUT_CLOSING_TAG} To switch underground view";
            string result = UIUtil.ColorizeDynamicKeybinds(testString, replacements);
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void Should_DynamicallyReplaceAndColorize_SingleValue_Having_TwoReplacements() {
            string testString = "[[Page Down]] To switch underground view";
            string[] replacements = new[] { "Key_X", "Key_Y" };
            string expected =
                $"{UConst.GetKeyboardShortcutColorTagOpener()}Key_X{UConst.KEYBOARD_SHORTCUT_CLOSING_TAG} To switch underground view";
            string result = UIUtil.ColorizeDynamicKeybinds(testString, replacements);
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void Should_DynamicallyReplaceAndColorize_FirstOfTwoValues_Having_OneReplacement() {
            string testString = "[[Page Up]] | [[Page Down]] To switch underground view";
            string[] replacements = new[] { "Key_X" };
            string expected =
                $"{UConst.GetKeyboardShortcutColorTagOpener()}Key_X{UConst.KEYBOARD_SHORTCUT_CLOSING_TAG} | [[Page Down]] To switch underground view";
            string result = UIUtil.ColorizeDynamicKeybinds(testString, replacements);
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void Should_DynamicallyReplaceAndColorize_TwoValues_Having_ThreeReplacements() {
            string testString = "[[Page Up]] | [[Page Down]] To switch underground view";
            string[] replacements = new[] { "Key_X", "Key_Y", "Key_Z" };
            string expected =
                $"{UConst.GetKeyboardShortcutColorTagOpener()}Key_X{UConst.KEYBOARD_SHORTCUT_CLOSING_TAG} | {UConst.GetKeyboardShortcutColorTagOpener()}Key_Y{UConst.KEYBOARD_SHORTCUT_CLOSING_TAG} To switch underground view";
            string result = UIUtil.ColorizeDynamicKeybinds(testString, replacements);
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void Should_Not_DynamicallyReplaceAndColorize_ZeroValues_Having_TwoReplacements() {
            string testString = "Nothing to replace";
            string[] replacements = new[] { "Key_X", "Key_Y"};
            string expected = "Nothing to replace";
            string result = UIUtil.ColorizeDynamicKeybinds(testString, replacements);
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void Should_Not_DynamicallyReplaceAndColorize_IncorrectValue_Having_TwoReplacements() {
            string testString = "Not replace this incorrect key [incorrect key]";
            string[] replacements = new[] { "Key_X", "Key_Y"};
            string expected = "Not replace this incorrect key [incorrect key]";
            string result = UIUtil.ColorizeDynamicKeybinds(testString, replacements);
            Assert.AreEqual(expected, result);
        }
    }
}