namespace TMUnitTest.Util {
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using TrafficManager.Util;

    [TestClass]
    public class TaskListTests {

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Constructor01_InvalidBlockSize_Should_ThrowException() {
            _ = new TaskList<int>(3); // invalid: should be power of 2
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Constructor02_ZeroBlockSize_Should_ThrowException() {
            _ = new TaskList<int>(0); // invalid: should be non-zero
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Constructor03_NegativeBlockSize_Should_ThrowException() {
            _ = new TaskList<int>(-8); // invalid: should be positive
        }

        [TestMethod]
        public void Constructor04_ValidBlockSize_Should_BeAccepted() {
            var t = new TaskList<int>(8);

            Assert.AreEqual(t.BlockSize, 8);
        }

        [TestMethod]
        public void TaskList01_TasksArray_Should_InitiallyBeNull() {
            var t = new TaskList<int>(8);

            Assert.IsNull(t.Tasks);
        }

        #region stuff for testing

        internal struct TaskStruct {
            public bool IsActive;
            public string ID;
        }

        internal TaskList<TaskStruct> TaskList;

        internal void OnAddItem(int index) {
            TaskList.Tasks[index].IsActive = true;
        }

        internal void OnDelItem(int index) {
            TaskList.Tasks[index].IsActive = false;
        }

        internal bool IsActive(int index) => TaskList.Tasks[index].IsActive;

        internal int BlockSize = 4;

        #endregion stuff for testing

        [TestMethod]
        public void TaskList02_AddingAnItem_Should_CreateValidTasksArray() {
            TaskList = new(BlockSize);

            TaskList.OnAddTask = OnAddItem;
            TaskList.OnRemoveTask = OnDelItem;
            TaskList.TaskIsActive = IsActive;

            TaskList.Add(new TaskStruct { ID = "0" });

            Assert.IsNotNull(TaskList.Tasks);

            Assert.AreEqual(TaskList.Tasks.Length, BlockSize);

            Assert.IsTrue(IsActive(0));

            Assert.AreEqual(TaskList.Size, 1);

            var expectedArray = new TaskStruct[BlockSize];

            expectedArray[0].ID = "0";
            expectedArray[0].IsActive = true;

            CollectionAssert.AreEqual(TaskList.Tasks, expectedArray);
        }

        [TestMethod]
        public void TaskList03_FillingCurrentBlock_Should_NotChangeArrayCapacity() {
            TaskList = new(BlockSize);

            TaskList.OnAddTask = OnAddItem;
            TaskList.OnRemoveTask = OnDelItem;
            TaskList.TaskIsActive = IsActive;

            for (int i = 0; i < BlockSize; i++) {
                TaskList.Add(new TaskStruct { ID = $"{i}" });
            }

            Assert.AreEqual(TaskList.Size, BlockSize);
            Assert.AreEqual(TaskList.Tasks.Length, BlockSize);
        }

        [TestMethod]
        public void TaskList04_WhenBlockIsFullAddingItem_Should_ExtendTheArray() {
            TaskList = new(BlockSize);

            TaskList.OnAddTask = OnAddItem;
            TaskList.OnRemoveTask = OnDelItem;
            TaskList.TaskIsActive = IsActive;

            for (int i = 0; i < BlockSize + 1; i++) {
                TaskList.Add(new TaskStruct { ID = $"{i}" });
            }

            Assert.AreEqual(TaskList.Size, BlockSize + 1);
            Assert.AreEqual(TaskList.Tasks.Length, 2 * BlockSize);

            var expectedArray = new TaskStruct[2 * BlockSize];

            for (int i = 0; i < BlockSize + 1; i++) {
                expectedArray[i] = new TaskStruct { ID = $"{i}", IsActive = true };
            }

            CollectionAssert.AreEqual(TaskList.Tasks, expectedArray);
        }

        [TestMethod]
        public void TaskList05_RemovingEndItem_Should_MarkItInactive() {
            TaskList = new(BlockSize);

            TaskList.OnAddTask = OnAddItem;
            TaskList.OnRemoveTask = OnDelItem;
            TaskList.TaskIsActive = IsActive;

            for (int i = 0; i < BlockSize; i++) {
                TaskList.Add(new TaskStruct { ID = $"{i}" });
            }

            Assert.AreEqual(TaskList.Size, BlockSize);
            Assert.AreEqual(TaskList.UnusedCapacity, 0);

            TaskList.RemoveAt(BlockSize - 1);

            Assert.AreEqual(TaskList.Size, BlockSize - 1);
            Assert.AreEqual(TaskList.UnusedCapacity, 1);
            Assert.IsFalse(TaskList.Tasks[TaskList.Size].IsActive);
        }

        [TestMethod]
        public void TaskList06_RemovingFirstItem_Should_ReplaceWithEndItemAndMarkEndItemInactive() {
            TaskList = new(BlockSize);

            TaskList.OnAddTask = OnAddItem;
            TaskList.OnRemoveTask = OnDelItem;
            TaskList.TaskIsActive = IsActive;

            for (int i = 0; i < BlockSize; i++) {
                TaskList.Add(new TaskStruct { ID = $"{i}" });
            }

            TaskList.RemoveAt(0);

            Assert.AreEqual(TaskList.Size, BlockSize - 1);
            Assert.AreEqual(TaskList.Tasks[0].ID, "3");
            Assert.AreEqual(TaskList.UnusedCapacity, 1);
        }
    }
}
