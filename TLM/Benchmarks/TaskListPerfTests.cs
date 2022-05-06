using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using TrafficManager.Util;

namespace Benchmarks {
    public class TaskListPerfTests {

        [GlobalSetup]
        public void Setup() {
            TestData.InitialiseLists();
            TestData.GenerateDeltas();

            TestData.testTaskList.EnsureCapacityFor(1);
            TestData.testFastList.EnsureCapacity(1);
        }

        [IterationCleanup]
        public void ClearLists() {
            //TestData.testTaskList.Clear();
            //TestData.testFastList.Clear();
        }

        [Benchmark]
        public void TaskList_01_Add6kItems_To_UnpreparedEmptyList() {
            var list = TestData.testTaskList;

            if (TestData.Deltas.TryGetValue("add6k", out var delta)) {
                var added = delta.Added;
                for (int i = 0; i < added.Length; i++) {
                    list.Add(new TestStruct { ID = added[i], IsActive = true });
                }
            }
        }

        [Benchmark]
        public void FastList_01_Add6kItems_To_UnpreparedEmptyList() {
            var list = TestData.testFastList;

            if (TestData.Deltas.TryGetValue("add6k", out var delta)) {
                var added = delta.Added;
                for (int i = 0; i < added.Length; i++) {
                    list.Add(new TestStruct { ID = added[i], IsActive = true });
                }
            }
        }

        [Benchmark]
        public void TaskList_02_Add6kItems_To_PreparedEmptyList() {
            var list = TestData.testTaskList;

            if (TestData.Deltas.TryGetValue("add6k", out var delta)) {
                var added = delta.Added;
                list.EnsureCapacityFor(added.Length);

                for (int i = 0; i < added.Length; i++) {
                    list.Add(new TestStruct { ID = added[i], IsActive = true });
                }
            }
        }

        [Benchmark]
        public void FastList_02_Add6kItems_To_PreparedEmptyList() {
            var list = TestData.testFastList;

            if (TestData.Deltas.TryGetValue("add6k", out var delta)) {
                var added = delta.Added;
                list.EnsureCapacity(added.Length);

                for (int i = 0; i < added.Length; i++) {
                    list.Add(new TestStruct { ID = added[i], IsActive = true });
                }
            }
        }

        [Benchmark]
        public void TaskList_03_ManuallyAdd6kItems_To_PreparedEmptyList() {
            var list = TestData.testTaskList;

            if (TestData.Deltas.TryGetValue("add6k", out var delta)) {
                var added = delta.Added;
                list.EnsureCapacityFor(added.Length);

                for (int i = 0; i < added.Length; i++) {
                    ref var task = ref list.Tasks[list.Size++];

                    task.ID = added[i];
                    task.IsActive = true;
                }
            }
        }

        [Benchmark]
        public void FastList_03_ManuallyAdd6kItems_To_PreparedEmptyList() {
            var list = TestData.testFastList;

            if (TestData.Deltas.TryGetValue("add6k", out var delta)) {
                var added = delta.Added;
                list.EnsureCapacity(list.m_size + added.Length);

                for (int i = 0; i < added.Length; i++) {
                    ref var task = ref list.m_buffer[list.m_size++];

                    task.ID = added[i];
                    task.IsActive = true;
                }
            }
        }

        [Benchmark]
        public void TaskList_04_AltManuallyAdd6kItems_To_PreparedEmptyList() {
            var list = TestData.testTaskList;

            if (TestData.Deltas.TryGetValue("add6k", out var delta)) {
                var added = delta.Added;
                list.EnsureCapacityFor(added.Length);

                for (int i = 0; i < added.Length; i++) {
                    int idx = list.Size++;
                    list.Tasks[idx].ID = added[i];
                    list.Tasks[idx].IsActive = true;
                }
            }
        }

        /*
        [GlobalSetup(Targets = new[] {
            nameof(TaskList_04_Del3kItems_From_ExistingList),
        })]
        public void Setup6k() {
            TestData.InitialiseLists();
            TestData.GenerateDeltas();

            var list = TestData.testTaskList;
            list.Clear(skipEvents: true);

            if (TestData.Deltas.TryGetValue("add6k", out var delta)) {
                var added = delta.Added;
                list.EnsureCapacityFor(added.Length);

                for (int i = 0; i < added.Length; i++) {
                    int idx = list.Size++;
                    list.Tasks[idx].ID = added[i];
                    list.Tasks[idx].IsActive = true;
                }
            }
        }


        [Benchmark]
        public void TaskList_04_Del3kItems_From_ExistingList() {
            var list = TestData.testTaskList;

            if (TestData.Deltas.TryGetValue("del3kadd3k", out var delta)) {
                var retained = delta.Retained;
                for (int i = list.Size; i-- > 0;) {
                    if (!retained.Contains(list.Tasks[i].ID)) {
                        list.RemoveAt(i);
                    }
                }
            }
        }
        */
    }

    /* --------------------- test data ---------------------  */

    internal struct TestStruct {
        public int ID;
        public bool IsActive;
    }

    internal struct Delta {
        public HashSet<int> Retained;
        public int[] Added;
    }

    internal static class TestData {
        internal static TaskList<TestStruct> testTaskList;
        internal static FastList<TestStruct> testFastList;

        internal static Dictionary<string, Delta> Deltas;

        private static int[] rawData;
        private static HashSet<int> previous;
        private static List<int> added;
        private static List<int> retained;

        internal static void InitialiseLists() {
            Console.WriteLine("// InitialiseLists()");

            // create test lists
            testTaskList = new TaskList<TestStruct>(32);
            testFastList = new FastList<TestStruct>();

            // delegates
            //testTaskList.OnAddTask = OnAddTL;
            testTaskList.OnRemoveTask = OnDelTL;
            testTaskList.TaskIsActive = IsActiveTL;

            // populate raw data with unique values
            rawData = new int[36864u];
            for (int i = 0; i < rawData.Length; i++) {
                rawData[i] = i;
            }

            previous = new HashSet<int>(0);
            added = new List<int>(4001);
            retained = new List<int>(2001);

            Deltas = new Dictionary<string, Delta>();
        }

        internal static void GenerateDeltas() {
            Console.WriteLine("// GenerateDeltas()");

            Deltas.Add("removeAll", new Delta { Retained = previous, Added = new int[0] });

            var center = rawData.Length / 2;
            var zoom = 3000;

            Deltas.Add("add6k", GenerateDelta(center, zoom));

            center -= zoom;

            Deltas.Add("del3kadd3k", GenerateDelta(center, zoom));
        }

        internal static Delta GenerateDelta(int center, int zoom) {
            Console.WriteLine($"// GenerateDeltas({center}, {zoom})");

            var viewport = rawData.AsSpan(center - zoom, zoom * 2).ToArray();

            added.Clear();
            retained.Clear();

            foreach (int item in viewport) {
                if (previous.Contains(item)) {
                    retained.Add(item);
                } else {
                    added.Add(item);
                }
            }

            previous = new HashSet<int>(viewport);

            return new Delta {
                Retained = new HashSet<int>(retained),
                Added = added.ToArray(),
            };
        }

        internal static void OnAddTL(int index) {
            testTaskList.Tasks[index].IsActive = true;
        }
        internal static void OnDelTL(int index) {
            testTaskList.Tasks[index].IsActive = false;
        }
        internal static bool IsActiveTL(int index) {
            return testTaskList.Tasks[index].IsActive;
        }
    }
}
