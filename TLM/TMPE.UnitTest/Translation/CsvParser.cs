namespace TMUnitTest.Translation {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using TrafficManager.UI.Localization;

    [TestClass]
    public class CsvParser {
        private static byte[] multilineTestBlock;
        private static byte[] testBlock;
        private static Dictionary<string, byte[]> multiLineTests;
        private static Dictionary<string, byte[]> basicParserTests;

        [ClassInitialize]
        public static void InitializeClass(TestContext testContext) {
            testBlock = File.ReadAllBytes("./Translation/TestFiles/TestBlock.txt");
            InitBasicLineParserTests();
            multilineTestBlock =
                File.ReadAllBytes("./Translation/TestFiles/MultilineTestBlock.txt");
            InitMultiLineParserTests();
        }

        private static void InitMultiLineParserTests() {
            multiLineTests = new Dictionary<string, byte[]>();
            ParseTestText(multilineTestBlock, multiLineTests);
        }

        private static void InitBasicLineParserTests() {
            basicParserTests = new Dictionary<string, byte[]>();
            ParseTestText(testBlock, basicParserTests);
        }

        /// <summary>
        /// Parse test cases from file
        /// </summary>
        /// <param name="blockOfTests"></param>
        /// <param name="testCases"></param>
        private static void ParseTestText(byte[] blockOfTests, Dictionary<string, byte[]> testCases) {
            using (var stream = new MemoryStream(blockOfTests)) {
                using (TextReader r = new StreamReader(stream)) {
                    string line;
                    string testName = "";
                    List<string> testLines = new List<string>();
                    while ((line = r.ReadLine()) != null) {
                        if (line.StartsWith("// -- Test")) {
                            string[] parts = line.Split(' ');
                            testName = parts[2] + "-" + parts[3];
                        } else if (line.StartsWith("// ------------")) {
                            string testText = string.Join(Environment.NewLine, testLines.ToArray());
                            testCases.Add(testName, Encoding.ASCII.GetBytes(testText));
                            testLines.Clear();
                        } else if (line.Length != 0) {
                            testLines.Add(line);
                        }
                    }
                }
            }
        }

        [TestInitialize]
        public void InitializeTest() {
            Console.WriteLine("InitializedTest");
        }


        [TestMethod]
        public void ParseTest1() {
            string[] expectedLines = {
                "\"\",\"de\",\"en\",\"fr\"",
                "\"language-columns\",\"value-de\",\"value-en\",\"value-fr\""
            };
            ReadAndJoinLines(basicParserTests["Test-1"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en", "fr"}, out resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("language-columns", "value-de"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("language-columns", "value-en"));
            CollectionAssert.Contains(resultMap["fr"], new KeyValuePair<string, string>("language-columns", "value-fr"));
        }

        [TestMethod]
        public void ParseTest2() {
            string[] expectedLines = {
                "\"\",\"de\",\"en\",\"en-gb\",\"fr\"",
                "\"language-columns-locale-code\",\"value-de\",\"value-en\",\"value-en-gb\",\"value-fr\""
            };
            ReadAndJoinLines(basicParserTests["Test-2"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en", "en-gb", "fr"}, out resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("language-columns-locale-code", "value-de"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("language-columns-locale-code", "value-en"));
            CollectionAssert.Contains(resultMap["en-gb"], new KeyValuePair<string, string>("language-columns-locale-code", "value-en-gb"));
            CollectionAssert.Contains(resultMap["fr"], new KeyValuePair<string, string>("language-columns-locale-code", "value-fr"));
        }

        [TestMethod]
        public void ParseTest3() {
            string[] expectedLines =
                {"\"\",\"de\",\"en\",\"fr\"", "\"empty-values-quote-marks\",\"\",\"\",\"\""};
            ReadAndJoinLines(basicParserTests["Test-3"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en", "fr"}, out resultMap);
            CollectionAssert.DoesNotContain(resultMap["de"], "empty-values-quote-marks");
            CollectionAssert.DoesNotContain(resultMap["en"], "empty-values-quote-marks");
            CollectionAssert.DoesNotContain(resultMap["fr"], "empty-values-quote-marks");
        }

        [TestMethod]
        public void ParseTest4() {
            string[] expectedLines = {
                "\"\",\"de\",\"en\",\"fr\"", "\"empty-values-quote-marks-mixed\",\"\",\"en\",\"\""
            };
            ReadAndJoinLines(basicParserTests["Test-4"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en", "fr"}, out resultMap);
            CollectionAssert.DoesNotContain(resultMap["de"], "empty-values-quote-marks-mixed");
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("empty-values-quote-marks-mixed", "en"));
            CollectionAssert.DoesNotContain(resultMap["fr"], "empty-values-quote-marks-mixed");
        }

        [TestMethod]
        public void ParseTest5() {
            string[] expectedLines =
                {"\"\",\"de\",\"en\",\"fr\"", "\"empty-values-no-quote-marks\",,,"};
            ReadAndJoinLines(basicParserTests["Test-5"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en", "fr"}, out resultMap);
            CollectionAssert.DoesNotContain(resultMap["de"], "empty-values-no-quote-marks");
            CollectionAssert.DoesNotContain(resultMap["en"], "empty-values-no-quote-marks");
            CollectionAssert.DoesNotContain(resultMap["fr"], "empty-values-no-quote-marks");
        }

        [TestMethod]
        public void ParseTest6() {
            string[] expectedLines = {
                "\"\",\"de\",\"en\",\"fr\"", "\"empty-values-no-quote-marks-mixed\",,,\"lang-fr\""
            };
            ReadAndJoinLines(basicParserTests["Test-6"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en", "fr"}, out resultMap);
            CollectionAssert.DoesNotContain(resultMap["de"], "empty-values-no-quote-marks-mixed");
            CollectionAssert.DoesNotContain(resultMap["en"], "empty-values-no-quote-marks-mixed");
            CollectionAssert.Contains(resultMap["fr"], new KeyValuePair<string, string>("empty-values-no-quote-marks-mixed", "lang-fr"));
        }

        [TestMethod]
        public void ParseTest7() {
            string[] expectedLines = {
                "\"\",\"de\",\"en\",\"fr\"", "\"empty-values-no-quote-marks-mixed\",,\"lang-en\","
            };
            ReadAndJoinLines(basicParserTests["Test-7"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en", "fr"}, out resultMap);
            CollectionAssert.DoesNotContain(resultMap["de"], "empty-values-no-quote-marks-mixed");
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("empty-values-no-quote-marks-mixed", "lang-en"));
            CollectionAssert.DoesNotContain(resultMap["fr"], "empty-values-no-quote-marks-mixed");
        }

        [TestMethod]
        public void ParseTest8() {
            string[] expectedLines = {
                "\"\",\"de\",\"en\",\"fr\"",
                "\"escape-sign-text\",\"\\test value de/\",\"\\test value en/\",\"\\test value fr/\""
            };
            ReadAndJoinLines(basicParserTests["Test-8"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en", "fr"}, out resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("escape-sign-text", "\\test value de/"));
        }

        [TestMethod]
        public void ParseTest9() {
            string[] expectedLines = {
                "\"\",\"de\",\"en\",\"fr\"",
                "\"text-contains-comma\",\"test value de\",\"test,value en\",\"test value fr\""
            };
            ReadAndJoinLines(basicParserTests["Test-9"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en", "fr"}, out resultMap);
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("text-contains-comma", "test,value en"));
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("text-contains-comma", "test value de"));
       }

        [TestMethod]
        public void ParseTest10() {
            string[] expectedLines = {
                "\"\",\"de\",\"en\",\"fr\"",
                "\"text-contains-quote-mark\",\"test \"\"value de\",\"test value en\",\"test value fr\""
            };
            ReadAndJoinLines(basicParserTests["Test-10"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en", "fr"}, out resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("text-contains-quote-mark", "test \"value de"));
            CollectionAssert.Contains(resultMap["fr"], new KeyValuePair<string, string>("text-contains-quote-mark", "test value fr"));
        }

        [TestMethod]
        public void ParseTest11() {
            string[] expectedLines = {
                "\"\",\"de\",\"en\",\"fr\"",
                "\"text-contains-quote-mark-multiple\",\"test \"\"value\"\" de\",\"test value en\",\"test value fr\""
            };
            ReadAndJoinLines(basicParserTests["Test-11"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en", "fr"}, out resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("text-contains-quote-mark-multiple", "test \"value\" de"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("text-contains-quote-mark-multiple", "test value en"));
        }

        private static void ReadAndJoinLines(byte[] testData,
                                                           out string[] validatedLines) {
            PrivateType lookupTableType = new PrivateType(typeof(LookupTable));
            using (MemoryStream m = new MemoryStream(testData)) {
                using (StreamReader stream = new StreamReader(m)) {
                    validatedLines = (string[])lookupTableType.InvokeStatic("ReadLines", stream);
                }
            }
        }

        //------------------------------------------------------------------------------------------
        //-----------------------------MULTI-LINE-CELL-TESTS----------------------------------------
        //------------------------------------------------------------------------------------------

        [TestMethod]
        public void MultiLineParseTest1() {
            string[] expectedLines = {"\"\",\"de\",\"en\"", "\"new-line-first-cell\",\"test\nde\",\"test_en\""};
            ReadAndJoinLines(multiLineTests["Test-1"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en"}, out resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("new-line-first-cell", "test\nde"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("new-line-first-cell", "test_en"));
        }

        [TestMethod]
        public void MultiLineParseTest2() {
            string[] expectedLines = {"\"\",\"de\",\"en\"", "\"new-line-both-cells\",\"test\nde\",\"test\nen\""};
            ReadAndJoinLines(multiLineTests["Test-2"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en"}, out resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("new-line-both-cells", "test\nde"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("new-line-both-cells", "test\nen"));
        }

        [TestMethod]
        public void MultiLineParseTest3() {
            string[] expectedLines = {"\"\",\"de\",\"en\"", "\"multiple-new-lines-first-cell\",\"test\nmultiple\nlines\",\"test en\""};
            ReadAndJoinLines(multiLineTests["Test-3"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en"}, out resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-first-cell", "test\nmultiple\nlines"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-first-cell", "test en"));
        }

        [TestMethod]
        public void MultiLineParseTest4() {
            string[] expectedLines = {"\"\",\"de\",\"en\"", "\"multiple-new-lines-first-cell-trailing\",\"test\nmultiple\nlines\n\",\"test en\""};
            ReadAndJoinLines(multiLineTests["Test-4"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en"}, out resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-first-cell-trailing", "test\nmultiple\nlines\n"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-first-cell-trailing", "test en"));
        }

        [TestMethod]
        public void MultiLineParseTest5() {
            string[] expectedLines = {"\"\",\"de\",\"en\"", "\"multiple-new-lines-both-cells\",\"test\nde\nmultiple\nlines\",\"test\nen\nmultiple\nlines\""};
            ReadAndJoinLines(multiLineTests["Test-5"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en"}, out resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-both-cells", "test\nde\nmultiple\nlines"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-both-cells", "test\nen\nmultiple\nlines"));
        }

        [TestMethod]
        public void MultiLineParseTest6() {
            string[] expectedLines = {"\"\",\"de\",\"en\"", "\"multiple-new-lines-first-cell-variable-word-count-per-line\",\"test\nmulti line text\nseparation\",\"test en\""};
            ReadAndJoinLines(multiLineTests["Test-6"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en"}, out resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-first-cell-variable-word-count-per-line", "test\nmulti line text\nseparation"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-first-cell-variable-word-count-per-line", "test en"));
        }

        [TestMethod]
        public void MultiLineParseTest7() {
            string[] expectedLines = {"\"\",\"de\",\"en\"", "\"multiple-new-lines-variable-spaces-multi-word\",\"test\nmulti    line    text\nseparation\",\"test en\""};
            ReadAndJoinLines(multiLineTests["Test-7"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en"}, out resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-variable-spaces-multi-word", "test\nmulti    line    text\nseparation"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-variable-spaces-multi-word", "test en"));
        }

        [TestMethod]
        public void MultiLineParseTest8() {
            string[] expectedLines = {"\"\",\"de\",\"en\"", "\"multiple-new-lines-contains-comma\",\"test\nmulti,line text\nseparation\",\"test en\""};
            ReadAndJoinLines(multiLineTests["Test-8"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en"}, out resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-contains-comma", "test\nmulti,line text\nseparation"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-contains-comma", "test en"));
        }

        [TestMethod]
        public void MultiLineParseTest9() {
            string[] expectedLines = {"\"\",\"de\",\"en\"", "\"multiple-new-lines-contains-comma-and-quote-marks\",\"test\nmulti,line text with \"\"quote-marked text\"\" and\nseparation\",\"test en\""};
            ReadAndJoinLines(multiLineTests["Test-9"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en"}, out resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-contains-comma-and-quote-marks", "test\nmulti,line text with \"quote-marked text\" and\nseparation"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-contains-comma-and-quote-marks", "test en"));
        }

        [TestMethod]
        public void MultiLineParseTest10() {
            string[] expectedLines = {
                "\"\",\"de\",\"en\"",
                "\"multiple-new-lines-contains-multiple-commas-and-quote-marks\",\"test\nmulti, line text with \"\"quote-marked text\"\", text between commas,\nseparation\",\"test en\"",
                "\"multiple-new-lines-contains-multiple-commas-and-quote-marks-2\",\"test\nmulti, line text with \"\"quote-marked\n text\"\", text between commas,\nseparation\",\"test\n en\"",
                "\"multiple-new-lines-contains-multiple-commas-and-quote-marks-3\",\"test \"\"\nline end with \"\"quote-marks\"\"\",\"test en\""
            };
            ReadAndJoinLines(multiLineTests["Test-10"], out string[] validatedLines);
            CollectionAssert.AreEqual(expectedLines, validatedLines);
            Dictionary<string, Dictionary<string, string>> resultMap = new Dictionary<string, Dictionary<string, string>>();
            TestCollectTranslations(validatedLines, new[] {"de", "en"}, out resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks", "test\nmulti, line text with \"quote-marked text\", text between commas,\nseparation"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks", "test en"));
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks-2", "test\nmulti, line text with \"quote-marked\n text\", text between commas,\nseparation"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks-2", "test\n en"));
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks-3", "test \"\nline end with \"quote-marks\""));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks-3", "test en"));
        }

        /// <summary>
        /// Runs original methid which should collect translations
        /// </summary>
        /// <param name="lines">collection of translation lines, one</param>
        /// <param name="langCodes">column names to collect translated strings</param>
        /// <param name="result">map of translations [langCode][key][translated_string]</param>
        private void TestCollectTranslations(string[] lines,
                                     string[] langCodes,
                                     out Dictionary<string, Dictionary<string, string>> result) {
            PrivateType privateType = new PrivateType(typeof(LookupTable));
            object[] args = new object[] {lines, new List<string>(langCodes), null};
            privateType.InvokeStatic("CollectTranslations", args);
            result = (Dictionary<string, Dictionary<string, string>>)args[2];
        }
    }
}