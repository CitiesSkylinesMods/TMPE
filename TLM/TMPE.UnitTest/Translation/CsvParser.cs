namespace TMUnitTest.Translation {
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using TrafficManager.UI.Localization;

    [TestClass]
    public class CsvParser {
        private static byte[] multilineTestBlock_LF;
        private static byte[] multilineTestBlock_CRLF;
        private static byte[] testBlock_LF;
        private static byte[] testBlock_CRLF;

        private static List<string> testBlock_LF_Columns;
        private static List<string> testBlock_CRLF_Columns;
        private static List<string> multilineTestBlock_LF_Columns;
        private static List<string> multilineTestBlock_CRLF_Columns;
        private static string testBlock_LF_DataBlock;
        private static string testBlock_CRLF_DataBlock;
        private static string multilineTestBlock_LF_DataBlock;
        private static string multilineTestBlock_CRLF_DataBlock;

        [ClassInitialize]
        public static void InitializeClass(TestContext testContext) {
            testBlock_LF = File.ReadAllBytes("./Translation/TestFiles/TestBlock_LF.test");
            testBlock_CRLF = File.ReadAllBytes("./Translation/TestFiles/TestBlock_CRLF.test");
            multilineTestBlock_LF = File.ReadAllBytes("./Translation/TestFiles/MultilineTestBlock_LF.test");
            multilineTestBlock_CRLF = File.ReadAllBytes("./Translation/TestFiles/MultilineTestBlock_CRLF.test");
            PrepareData(testBlock_LF, out testBlock_LF_Columns, out testBlock_LF_DataBlock);
            PrepareData(testBlock_CRLF, out testBlock_CRLF_Columns, out testBlock_CRLF_DataBlock);
            PrepareData(multilineTestBlock_LF, out multilineTestBlock_LF_Columns, out multilineTestBlock_LF_DataBlock);
            PrepareData(multilineTestBlock_CRLF, out multilineTestBlock_CRLF_Columns, out multilineTestBlock_CRLF_DataBlock);
        }

        private static void PrepareData(byte[] testFileData, out List<string> columns, out string dataBlock) {
            PrivateType lookupTableType = new PrivateType(typeof(LookupTable));
            string columnsRow;
            using (var m = new MemoryStream(testFileData)) {
                using (var sr = new StreamReader(m)) {
                    object[] args = {sr, null, null};
                    lookupTableType.InvokeStatic("ReadLines", args);
                    columnsRow = (string)args[1];
                    dataBlock = (string)args[2];
                }
            }
            columns = new List<string>();
            using (var sr = new StringReader(columnsRow)) {
                lookupTableType.InvokeStatic("ReadCsvCell", sr); // skip first cell
                while (true) {
                    string colName = (string)lookupTableType.InvokeStatic("ReadCsvCell", sr);
                    if (colName.Length == 0) {
                        break;
                    }
                    columns.Add(colName);
                }
            }
        }

        [TestMethod]
        public void TestFirstRow_LF() {
            TestCollectTranslations(testBlock_LF_DataBlock, testBlock_LF_Columns, out Dictionary<string, Dictionary<string, string>> resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("language-columns", "value-de"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("language-columns", "value-en"));
            CollectionAssert.Contains(resultMap["fr"], new KeyValuePair<string, string>("language-columns", "value-fr"));
        }

        [TestMethod]
        public void TestFirstRow_CRLF() {
            TestCollectTranslations(testBlock_CRLF_DataBlock, testBlock_CRLF_Columns, out Dictionary<string, Dictionary<string, string>> resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("language-columns", "value-de"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("language-columns", "value-en"));
            CollectionAssert.Contains(resultMap["fr"], new KeyValuePair<string, string>("language-columns", "value-fr"));
        }

        [TestMethod]
        public void TestEmptyCells_LF() {
            TestCollectTranslations(testBlock_LF_DataBlock, testBlock_LF_Columns, out Dictionary<string, Dictionary<string, string>> resultMap);
            CollectionAssert.DoesNotContain(resultMap["de"], "empty-values-quote-marks");
            CollectionAssert.DoesNotContain(resultMap["en"], "empty-values-quote-marks");
            CollectionAssert.DoesNotContain(resultMap["fr"], "empty-values-quote-marks");

            CollectionAssert.DoesNotContain(resultMap["de"], "empty-values-quote-marks-mixed");
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("empty-values-quote-marks-mixed", "en"));
            CollectionAssert.DoesNotContain(resultMap["fr"], "empty-values-quote-marks-mixed");

            CollectionAssert.DoesNotContain(resultMap["de"], "empty-values-no-quote-marks");
            CollectionAssert.DoesNotContain(resultMap["en"], "empty-values-no-quote-marks");
            CollectionAssert.DoesNotContain(resultMap["fr"], "empty-values-no-quote-marks");

            CollectionAssert.DoesNotContain(resultMap["de"], "empty-values-no-quote-marks-mixed");
            CollectionAssert.DoesNotContain(resultMap["en"], "empty-values-no-quote-marks-mixed");
            CollectionAssert.Contains(resultMap["fr"], new KeyValuePair<string, string>("empty-values-no-quote-marks-mixed", "lang-fr"));
        }

        [TestMethod]
        public void TestEmptyCells_CRLF() {
            TestCollectTranslations(testBlock_CRLF_DataBlock, testBlock_CRLF_Columns, out Dictionary<string, Dictionary<string, string>> resultMap);
            CollectionAssert.DoesNotContain(resultMap["de"], "empty-values-quote-marks");
            CollectionAssert.DoesNotContain(resultMap["en"], "empty-values-quote-marks");
            CollectionAssert.DoesNotContain(resultMap["fr"], "empty-values-quote-marks");

            CollectionAssert.DoesNotContain(resultMap["de"], "empty-values-quote-marks-mixed");
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("empty-values-quote-marks-mixed", "en"));
            CollectionAssert.DoesNotContain(resultMap["fr"], "empty-values-quote-marks-mixed");

            CollectionAssert.DoesNotContain(resultMap["de"], "empty-values-no-quote-marks");
            CollectionAssert.DoesNotContain(resultMap["en"], "empty-values-no-quote-marks");
            CollectionAssert.DoesNotContain(resultMap["fr"], "empty-values-no-quote-marks");

            CollectionAssert.DoesNotContain(resultMap["de"], "empty-values-no-quote-marks-mixed");
            CollectionAssert.DoesNotContain(resultMap["en"], "empty-values-no-quote-marks-mixed");
            CollectionAssert.Contains(resultMap["fr"], new KeyValuePair<string, string>("empty-values-no-quote-marks-mixed", "lang-fr"));
        }

        [TestMethod]
        public void TestMixedCells_LF() {
            TestCollectTranslations( testBlock_LF_DataBlock, testBlock_LF_Columns, out Dictionary<string, Dictionary<string, string>> resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("text-contains-comma", "test value de"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("text-contains-comma", "test,value en"));
            CollectionAssert.Contains(resultMap["fr"], new KeyValuePair<string, string>("text-contains-comma", "test value fr"));

            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("text-contains-quote-mark", "test \"value de"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("text-contains-quote-mark", "test value en"));
            CollectionAssert.Contains(resultMap["fr"], new KeyValuePair<string, string>("text-contains-quote-mark", "test value fr"));

            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("text-contains-quote-mark-multiple", "test \"value\" de"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("text-contains-quote-mark-multiple", "test value en"));
            CollectionAssert.Contains(resultMap["fr"], new KeyValuePair<string, string>("text-contains-quote-mark-multiple", "test value fr"));
        }

        [TestMethod]
        public void TestMixedCells_CRLF() {
            TestCollectTranslations( testBlock_CRLF_DataBlock, testBlock_CRLF_Columns, out Dictionary<string, Dictionary<string, string>> resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("text-contains-comma", "test value de"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("text-contains-comma", "test,value en"));
            CollectionAssert.Contains(resultMap["fr"], new KeyValuePair<string, string>("text-contains-comma", "test value fr"));

            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("text-contains-quote-mark", "test \"value de"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("text-contains-quote-mark", "test value en"));
            CollectionAssert.Contains(resultMap["fr"], new KeyValuePair<string, string>("text-contains-quote-mark", "test value fr"));

            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("text-contains-quote-mark-multiple", "test \"value\" de"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("text-contains-quote-mark-multiple", "test value en"));
            CollectionAssert.Contains(resultMap["fr"], new KeyValuePair<string, string>("text-contains-quote-mark-multiple", "test value fr"));
        }

        [TestMethod]
        public void TestLastRowCells_LF() {
            TestCollectTranslations( testBlock_LF_DataBlock, testBlock_LF_Columns, out Dictionary<string, Dictionary<string, string>> resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("last-row", "last row column de"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("last-row", "last row column en"));
            CollectionAssert.Contains(resultMap["fr"], new KeyValuePair<string, string>("last-row", "last row column fr"));
        }

        [TestMethod]
        public void TestLastRowCells_CRLF() {
            TestCollectTranslations( testBlock_CRLF_DataBlock, testBlock_CRLF_Columns, out Dictionary<string, Dictionary<string, string>> resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("last-row", "last row column de"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("last-row", "last row column en"));
            CollectionAssert.Contains(resultMap["fr"], new KeyValuePair<string, string>("last-row", "last row column fr"));
        }

        //------------------------------------------------------------------------------------------
        //-----------------------------MULTI-LINE-CELL-TESTS----------------------------------------
        //------------------------------------------------------------------------------------------

        [TestMethod]
        public void TestMultiLineCellParse_LF() {
            TestCollectTranslations(multilineTestBlock_LF_DataBlock, multilineTestBlock_LF_Columns, out Dictionary<string, Dictionary<string, string>> resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("new-line-first-cell", "test\nde"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("new-line-first-cell", "test_en"));

            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("new-line-both-cells", "test\nde"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("new-line-both-cells", "test\nen"));

            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-first-cell", "test\nmultiple\nlines"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-first-cell", "test en"));

            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-first-cell-trailing", "test\nmultiple\nlines\n"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-first-cell-trailing", "test en"));
        }

        [TestMethod]
        public void TestMultiLineCellParse_CRLF() {
            TestCollectTranslations(multilineTestBlock_CRLF_DataBlock, multilineTestBlock_CRLF_Columns, out Dictionary<string, Dictionary<string, string>> resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("new-line-first-cell", "test\r\nde"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("new-line-first-cell", "test_en"));

            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("new-line-both-cells", "test\r\nde"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("new-line-both-cells", "test\r\nen"));

            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-first-cell", "test\r\nmultiple\r\nlines"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-first-cell", "test en"));

            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-first-cell-trailing", "test\r\nmultiple\r\nlines\r\n"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-first-cell-trailing", "test en"));
        }

        [TestMethod]
        public void TestMultiLineCellParseVariableSpacesAndMultiWord_LF() {
            TestCollectTranslations( multilineTestBlock_LF_DataBlock, multilineTestBlock_LF_Columns, out Dictionary<string, Dictionary<string, string>> resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-both-cells", "test\nde\nmultiple\nlines"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-both-cells", "test\nen\nmultiple\nlines"));

            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-first-cell-variable-word-count-per-line", "test\nmulti line text\nseparation"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-first-cell-variable-word-count-per-line", "test en"));

            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-variable-spaces-multi-word", "test\nmulti    line    text\nseparation"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-variable-spaces-multi-word", "test en"));
        }

        [TestMethod]
        public void TestMultiLineCellParseVariableSpacesAndMultiWord_CRLF() {
            TestCollectTranslations( multilineTestBlock_CRLF_DataBlock, multilineTestBlock_CRLF_Columns, out Dictionary<string, Dictionary<string, string>> resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-both-cells", "test\r\nde\r\nmultiple\r\nlines"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-both-cells", "test\r\nen\r\nmultiple\r\nlines"));

            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-first-cell-variable-word-count-per-line", "test\r\nmulti line text\r\nseparation"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-first-cell-variable-word-count-per-line", "test en"));

            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-variable-spaces-multi-word", "test\r\nmulti    line    text\r\nseparation"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-variable-spaces-multi-word", "test en"));
        }

        [TestMethod]
        public void TestMultiLineCellParseWithCommas_LF() {
            TestCollectTranslations(multilineTestBlock_LF_DataBlock, multilineTestBlock_LF_Columns, out Dictionary<string, Dictionary<string, string>> resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-contains-comma", "test\nmulti,line text\nseparation"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-contains-comma", "test en"));

            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-contains-comma-and-quote-marks", "test\nmulti,line text with \"quote-marked text\" and\nseparation"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-contains-comma-and-quote-marks", "test en"));
        }

        [TestMethod]
        public void TestMultiLineCellParseWithCommas_CRLF() {
            TestCollectTranslations(multilineTestBlock_CRLF_DataBlock, multilineTestBlock_CRLF_Columns, out Dictionary<string, Dictionary<string, string>> resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-contains-comma", "test\r\nmulti,line text\r\nseparation"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-contains-comma", "test en"));

            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-contains-comma-and-quote-marks", "test\r\nmulti,line text with \"quote-marked text\" and\r\nseparation"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-contains-comma-and-quote-marks", "test en"));
        }

        [TestMethod]
        public void TestMultiLineCellParseWithVariousData_LF() {
            TestCollectTranslations(multilineTestBlock_LF_DataBlock, multilineTestBlock_LF_Columns, out Dictionary<string, Dictionary<string, string>> resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks", "test\nmulti, line text with \"quote-marked text\", text between commas,\nseparation"));
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks-2", "test\nmulti, line text with \"quote-marked\n text\", text between commas,\nseparation"));
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks-3", "test \"\nline end with \"quote-marks\""));

            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks", "test en"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks-2", "test"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks-3", "test en"));
        }

        [TestMethod]
        public void TestMultiLineCellParseWithVariousData_CRLF() {
            TestCollectTranslations(multilineTestBlock_CRLF_DataBlock, multilineTestBlock_CRLF_Columns, out Dictionary<string, Dictionary<string, string>> resultMap);
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks", "test\r\nmulti, line text with \"quote-marked text\", text between commas,\r\nseparation"));
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks-2", "test\r\nmulti, line text with \"quote-marked\r\n text\", text between commas,\r\nseparation"));
            CollectionAssert.Contains(resultMap["de"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks-3", "test \"\r\nline end with \"quote-marks\""));

            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks", "test en"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks-2", "test"));
            CollectionAssert.Contains(resultMap["en"], new KeyValuePair<string, string>("multiple-new-lines-contains-multiple-commas-and-quote-marks-3", "test en"));
        }

        /// <summary>
        /// Runs original method which should collect translations
        /// </summary>
        /// <param name="dataBlock"></param>
        /// <param name="columns"></param>
        /// <param name="result">map of translations [langCode][key][translated_string]</param>
        private static void TestCollectTranslations(
            string dataBlock,
            List<string> columns,
            out Dictionary<string,
            Dictionary<string, string>> result)
        {
            PrivateType privateType = new PrivateType(typeof(LookupTable));
            object[] args = {dataBlock, columns, null};
            privateType.InvokeStatic("CollectTranslations", args);
            result = (Dictionary<string, Dictionary<string, string>>)args[2];
        }
    }
}