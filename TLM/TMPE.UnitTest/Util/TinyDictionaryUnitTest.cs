using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TrafficManager.Util;
using TrafficManager.Traffic;

namespace TMUnitTest.Util {
	[TestClass]
	public class TinyDictionaryUnitTest {
		private TinyDictionary<byte, byte> dict0;
		private TinyDictionary<string, int> dict1;
		private TinyDictionary<string, IList<string>> dict2;
		private TinyDictionary<ICollection<string>, bool> dict3;
		private TinyDictionary<ushort, IDictionary<ushort, ArrowDirection>> dict4;

		private static string alice, bob, cedric, dora;
		private static IList<string> alicesNicknames, bobsNicknames, cedricsNicknames, dorasNicknames;
		private static ICollection<string> girls, boys;

		#region Zusätzliche Testattribute
		//
		// Sie können beim Schreiben der Tests folgende zusätzliche Attribute verwenden:
		//
		// Verwenden Sie ClassInitialize, um vor Ausführung des ersten Tests in der Klasse Code auszuführen.
		// [ClassInitialize()]
		// public static void MyClassInitialize(TestContext testContext) { }
		//
		// Verwenden Sie ClassCleanup, um nach Ausführung aller Tests in einer Klasse Code auszuführen.
		// [ClassCleanup()]
		// public static void MyClassCleanup() { }
		//
		// Mit TestInitialize können Sie vor jedem einzelnen Test Code ausführen. 
		// [TestInitialize()]
		// public void MyTestInitialize() { }
		//
		// Mit TestCleanup können Sie nach jedem Test Code ausführen.
		// [TestCleanup()]
		// public void MyTestCleanup() { }
		//
		#endregion

		[ClassInitialize]
		public static void InitializeClass(TestContext testContext) {
			alice = "Alice";
			bob = "Bob";
			cedric = "Cedric";
			dora = "Dora";

			alicesNicknames = null;
			bobsNicknames = new List<string> { "Bobby", "Bobbi", "Bobb" };
			cedricsNicknames = new List<string> { "Ced" };
			dorasNicknames = new List<string>();

			girls = new HashSet<string> { alice, dora };
			boys = new HashSet<string> { bob, cedric };
		}

		[TestInitialize()]
		public void InitializeTest() {
			dict0 = new TinyDictionary<byte, byte>();

			dict1 = new TinyDictionary<string, int>();
			dict1.Add(alice, 1);
			dict1.Add(bob, 2);
			dict1.Add(cedric, 3);

			dict2 = new TinyDictionary<string, IList<string>>();
			dict2.Add(bob, bobsNicknames);
			dict2.Add(cedric, cedricsNicknames);
			dict2.Add(dora, dorasNicknames);
			dict2.Add(alice, alicesNicknames);

			dict3 = new TinyDictionary<ICollection<string>, bool>();
			dict3.Add(girls, true);
			dict3.Add(boys, false);
		}

		[TestMethod]
		public void TestKeys0() {
			ICollection<byte> keys = dict0.Keys;
			Assert.IsNotNull(keys);
			Assert.AreEqual(0, keys.Count);
		}

		[TestMethod]
		public void TestKeys1() {
			ICollection<string> keys = dict1.Keys;
			Assert.IsNotNull(keys);
			Assert.AreEqual(3, keys.Count);
			Assert.IsTrue(keys.Contains(alice));
			Assert.IsTrue(keys.Contains(bob));
			Assert.IsTrue(keys.Contains(cedric));
		}

		[TestMethod]
		public void TestKeys2() {
			ICollection<string> keys = dict2.Keys;
			Assert.IsNotNull(keys);
			Assert.AreEqual(4, keys.Count);
			Assert.IsTrue(keys.Contains(alice));
			Assert.IsTrue(keys.Contains(bob));
			Assert.IsTrue(keys.Contains(cedric));
			Assert.IsTrue(keys.Contains(dora));
		}

		[TestMethod]
		public void TestKeys3() {
			ICollection<ICollection<string>> keys = dict3.Keys;
			Assert.IsNotNull(keys);
			Assert.AreEqual(2, keys.Count);
			Assert.IsTrue(keys.Contains(girls));
			Assert.IsTrue(keys.Contains(boys));
		}

		[TestMethod]
		public void TestValues0() {
			ICollection<byte> values = dict0.Values;
			Assert.IsNotNull(values);
			Assert.AreEqual(0, values.Count);
		}

		[TestMethod]
		public void TestValues1() {
			ICollection<int> values = dict1.Values;
			Assert.IsNotNull(values);
			Assert.AreEqual(3, values.Count);
			Assert.IsTrue(values.Contains(1));
			Assert.IsTrue(values.Contains(2));
			Assert.IsTrue(values.Contains(3));
		}

		[TestMethod]
		public void TestValues2() {
			ICollection<IList<string>> values = dict2.Values;
			Assert.IsNotNull(values);
			Assert.AreEqual(4, values.Count);
			Assert.IsTrue(values.Contains(alicesNicknames));
			Assert.IsTrue(values.Contains(bobsNicknames));
			Assert.IsTrue(values.Contains(cedricsNicknames));
			Assert.IsTrue(values.Contains(dorasNicknames));
		}

		[TestMethod]
		public void TestValues3() {
			ICollection<bool> values = dict3.Values;
			Assert.IsNotNull(values);
			Assert.AreEqual(2, values.Count);
			Assert.IsTrue(values.Contains(true));
			Assert.IsTrue(values.Contains(false));
		}

		[TestMethod]
		[ExpectedException(typeof(ArgumentNullException))]
		public void TestGetNull() {
			int val = dict1[null];
		}

		[TestMethod]
		[ExpectedException(typeof(KeyNotFoundException))]
		public void TestGetNotFound() {
			int val = dict1["Santa Claus"];
		}

		[TestMethod]
		public void TestGet1() {
			int val = dict1[cedric];
			Assert.AreEqual(3, val);
		}

		[TestMethod]
		public void TestGet2() {
			IList<string> val = dict2[alice];
			Assert.AreEqual(alicesNicknames, val);

			val = dict2[bob];
			Assert.AreEqual(bobsNicknames, val);

			val = dict2[cedric];
			Assert.AreEqual(cedricsNicknames, val);

			val = dict2[dora];
			Assert.AreEqual(dorasNicknames, val);
		}

		[TestMethod]
		public void TestGet3() {
			bool val = dict3[girls];
			Assert.IsTrue(val);
		}

		[TestMethod]
		public void TestSet0() {
			dict0[1] = 2;
			Assert.AreEqual(dict0[1], 2);
		}

		[TestMethod]
		public void TestSet1() {
			dict1["Eugen"] = 42;
			Assert.AreEqual(dict1["Eugen"], 42);
		}

		[TestMethod]
		public void TestSet2() {
			IList<string> eugensNicknames = new List<string> { "Eugenius" };
			dict2["Eugen"] = eugensNicknames;
			Assert.AreEqual(dict2["Eugen"], eugensNicknames);
		}

		[TestMethod]
		public void TestSet3() {
			dict3[girls] = false;
			Assert.AreEqual(dict3[girls], false);
			Assert.AreEqual(dict3[boys], false);
		}

		[TestMethod]
		public void TestContainsKey0() {
			Assert.IsFalse(dict0.ContainsKey(0));
		}

		[TestMethod]
		public void TestContainsKey1() {
			Assert.IsTrue(dict1.ContainsKey(alice));
			Assert.IsTrue(dict1.ContainsKey(bob));
			Assert.IsTrue(dict1.ContainsKey(cedric));
			Assert.IsFalse(dict1.ContainsKey(dora));
		}

		[TestMethod]
		public void TestContainsKey2() {
			Assert.IsTrue(dict2.ContainsKey(alice));
			Assert.IsTrue(dict2.ContainsKey(bob));
			Assert.IsTrue(dict2.ContainsKey(cedric));
			Assert.IsTrue(dict2.ContainsKey(dora));
			Assert.IsFalse(dict2.ContainsKey("Eugen"));
		}

		[TestMethod]
		public void TestContainsKey3() {
			Assert.IsTrue(dict3.ContainsKey(girls));
			Assert.IsTrue(dict3.ContainsKey(boys));
			Assert.IsFalse(dict3.ContainsKey(new HashSet<string> { "Chair" }));
			Assert.IsFalse(dict3.ContainsKey(null));
		}

		[TestMethod]
		public void TestAddRemove() {
			dict0.Add(1, 5);
			dict0.Add(5, 1);
			dict0.Add(2, 2);
			Assert.AreEqual(3, dict0.Count);
			Assert.AreEqual(5, dict0[1]);
			Assert.AreEqual(1, dict0[5]);
			Assert.AreEqual(2, dict0[2]);
			dict0.Remove(1);
			Assert.AreEqual(2, dict0.Count);
			Assert.IsFalse(dict0.ContainsKey(1));
			dict0.Add(2, 3);
			Assert.AreEqual(2, dict0.Count);
			Assert.AreEqual(3, dict0[2]);
			dict0.Add(3, 0);
			Assert.AreEqual(3, dict0.Count);
			Assert.AreEqual(0, dict0[3]);
			dict0.Add(1, 4);
			Assert.AreEqual(4, dict0.Count);
			Assert.AreEqual(4, dict0[1]);
			dict0.Add(1, 8);
			Assert.AreEqual(4, dict0.Count);
			Assert.AreEqual(8, dict0[1]);
			dict0.Remove(1);
			Assert.AreEqual(3, dict0.Count);
			dict0.Add(1, 2);
			Assert.AreEqual(4, dict0.Count);
			Assert.AreEqual(2, dict0[1]);
			Assert.AreEqual(3, dict0[2]);
			Assert.AreEqual(0, dict0[3]);
			Assert.AreEqual(1, dict0[5]);
		}

		[TestMethod]
		public void TestTryGetValue1() {
			int val;
			Assert.IsTrue(dict1.TryGetValue(bob, out val));
			Assert.AreEqual(2, val);

			Assert.IsFalse(dict1.TryGetValue(dora, out val));
			Assert.AreEqual(default(int), val);
		}

		[TestMethod]
		public void TestClear() {
			dict1.Clear();
			Assert.AreEqual(0, dict1.Count);
		}

		[TestMethod]
		public void TestCopyTo() {
			KeyValuePair<string, IList<string>>[] a = new KeyValuePair<string, IList<string>>[4];
			dict2.CopyTo(a, 0);

			bool[] r = new bool[4];
			foreach (KeyValuePair<string, IList<string>> e in a) {
				if (alice.Equals(e.Key) && e.Value == null) {
					r[0] = true;
				} else if (bob.Equals(e.Key) && bobsNicknames.Equals(e.Value)) {
					r[1] = true;
				} else if (cedric.Equals(e.Key) && cedricsNicknames.Equals(e.Value)) {
					r[2] = true;
				} else if (dora.Equals(e.Key) && dorasNicknames.Equals(e.Value)) {
					r[3] = true;
				}
			}

			foreach (bool rr in r) {
				Assert.IsTrue(rr);
			}
		}

		[TestMethod]
		public void TestEnumeration() {
			int i = 0;
			bool[] r = new bool[3];
			foreach (KeyValuePair<string, int> e in dict1) {
				if (alice.Equals(e.Key)) {
					Assert.AreEqual(1, e.Value);
				} else if (bob.Equals(e.Key)) {
					Assert.AreEqual(2, e.Value);
				} else if (cedric.Equals(e.Key)) {
					Assert.AreEqual(3, e.Value);
				}
				r[e.Value - 1] = true;
				++i;
			}
			Assert.AreEqual(3, i);

			foreach (bool rr in r) {
				Assert.IsTrue(rr);
			}
		}

		[TestMethod]
		public void TestEnumerationAfterModification() {
			dict1[alice] = 5;

			int i = 0;
			bool[] r = new bool[3];
			foreach (KeyValuePair<string, int> e in dict1) {
				if (alice.Equals(e.Key)) {
					Assert.AreEqual(5, e.Value);
					r[0] = true;
				} else if (bob.Equals(e.Key)) {
					Assert.AreEqual(2, e.Value);
					r[1] = true;
				} else if (cedric.Equals(e.Key)) {
					Assert.AreEqual(3, e.Value);
					r[2] = true;
				}
				++i;
			}
			Assert.AreEqual(3, i);

			foreach (bool rr in r) {
				Assert.IsTrue(rr);
			}
		}

		[TestMethod]
		public void TestEnumerationAfterRemoval() {
			dict1.Remove(alice);

			int i = 0;
			bool[] r = new bool[2];
			foreach (KeyValuePair<string, int> e in dict1) {
				if (bob.Equals(e.Key)) {
					Assert.AreEqual(2, e.Value);
					r[0] = true;
				} else if (cedric.Equals(e.Key)) {
					Assert.AreEqual(3, e.Value);
					r[1] = true;
				}
				++i;
			}
			Assert.AreEqual(2, i);

			foreach (bool rr in r) {
				Assert.IsTrue(rr);
			}
		}

		[TestMethod]
		public void TestIncrement() {
			++dict1[alice];
			Assert.AreEqual(2, dict1[alice]);
			Assert.AreEqual(2, dict1[bob]);
			Assert.AreEqual(3, dict1[cedric]);
		}

		[TestMethod]
		public void TestArithmeticAssignment() {
			dict1[bob] *= 4;
			Assert.AreEqual(1, dict1[alice]);
			Assert.AreEqual(8, dict1[bob]);
			Assert.AreEqual(3, dict1[cedric]);
		}

		[TestMethod]
		[ExpectedException(typeof(KeyNotFoundException))]
		public void TestArithmeticAssignmentOnNonExistingKey() {
			dict1[dora]++;
		}

		[TestMethod]
		public void TestNested() {
			dict4 = new TinyDictionary<ushort, IDictionary<ushort, ArrowDirection>>();

			IDictionary<ushort, ArrowDirection> innerDict1 = new TinyDictionary<ushort, ArrowDirection>();
			dict4.Add(1270, innerDict1);
			innerDict1.Add(1270, ArrowDirection.Turn);
			innerDict1.Add(14929, ArrowDirection.Forward);
			innerDict1.Add(26395, ArrowDirection.Left);

			IDictionary<ushort, ArrowDirection> innerDict2 = new TinyDictionary<ushort, ArrowDirection>();
			dict4.Add(14929, innerDict2);
			innerDict2.Add(1270, ArrowDirection.Forward);
			innerDict2.Add(14929, ArrowDirection.Turn);
			innerDict2.Add(26395, ArrowDirection.Right);

			IDictionary<ushort, ArrowDirection> innerDict3 = new TinyDictionary<ushort, ArrowDirection>();
			dict4.Add(26395, innerDict3);
			innerDict3.Add(1270, ArrowDirection.Right);
			innerDict3.Add(14929, ArrowDirection.Left);
			innerDict3.Add(26395, ArrowDirection.Turn);

			Assert.AreEqual(3, dict4.Count);

			Assert.IsTrue(dict4.ContainsKey(1270));
			Assert.IsTrue(dict4.ContainsKey(14929));
			Assert.IsTrue(dict4.ContainsKey(26395));

			Assert.AreEqual(3, dict4[1270].Count);
			Assert.AreEqual(3, dict4[14929].Count);
			Assert.AreEqual(3, dict4[26395].Count);

			Assert.IsTrue(dict4[1270].ContainsKey(1270));
			Assert.IsTrue(dict4[1270].ContainsKey(14929));
			Assert.IsTrue(dict4[1270].ContainsKey(26395));

			Assert.IsTrue(dict4[14929].ContainsKey(1270));
			Assert.IsTrue(dict4[14929].ContainsKey(14929));
			Assert.IsTrue(dict4[14929].ContainsKey(26395));

			Assert.IsTrue(dict4[26395].ContainsKey(1270));
			Assert.IsTrue(dict4[26395].ContainsKey(14929));
			Assert.IsTrue(dict4[26395].ContainsKey(26395));

			Assert.AreEqual(ArrowDirection.Turn, dict4[1270][1270]);
			Assert.AreEqual(ArrowDirection.Forward, dict4[1270][14929]);
			Assert.AreEqual(ArrowDirection.Left, dict4[1270][26395]);

			Assert.AreEqual(ArrowDirection.Forward, dict4[14929][1270]);
			Assert.AreEqual(ArrowDirection.Turn, dict4[14929][14929]);
			Assert.AreEqual(ArrowDirection.Right, dict4[14929][26395]);

			Assert.AreEqual(ArrowDirection.Right, dict4[26395][1270]);
			Assert.AreEqual(ArrowDirection.Left, dict4[26395][14929]);
			Assert.AreEqual(ArrowDirection.Turn, dict4[26395][26395]);
		}
	}
}
