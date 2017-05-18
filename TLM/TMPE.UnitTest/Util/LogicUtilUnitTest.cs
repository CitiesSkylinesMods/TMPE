using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Util;

namespace TMUnitTest.Util {
	[TestClass]
	public class LogicUtilUnitTest {
		[TestMethod]
		public void TestCheckFlags1() {
			Assert.IsTrue(LogicUtil.CheckFlags((uint)(NetSegment.Flags.Created | NetSegment.Flags.Deleted), (uint)NetSegment.Flags.Created));
		}

		[TestMethod]
		public void TestCheckFlags2() {
			Assert.IsFalse(LogicUtil.CheckFlags((uint)(NetSegment.Flags.Created | NetSegment.Flags.Deleted), (uint)NetSegment.Flags.Collapsed));
		}

		[TestMethod]
		public void TestCheckFlags3() {
			Assert.IsTrue(LogicUtil.CheckFlags((uint)(NetSegment.Flags.Created | NetSegment.Flags.Collapsed), (uint)NetSegment.Flags.Created, (uint)NetSegment.Flags.Created));
		}

		[TestMethod]
		public void TestCheckFlags4() {
			Assert.IsTrue(LogicUtil.CheckFlags((uint)(NetSegment.Flags.Created | NetSegment.Flags.Collapsed), (uint)(NetSegment.Flags.Created | NetSegment.Flags.Deleted), (uint)NetSegment.Flags.Created));
		}

		[TestMethod]
		public void TestCheckFlags5() {
			Assert.IsFalse(LogicUtil.CheckFlags((uint)(NetSegment.Flags.Created | NetSegment.Flags.Deleted | NetSegment.Flags.Collapsed), (uint)(NetSegment.Flags.Created | NetSegment.Flags.Deleted), (uint)NetSegment.Flags.Created));
		}
	}
}
