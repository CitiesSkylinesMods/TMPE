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
        }

        [Benchmark]
        public void TaskList_SimulatedCamMoves() {

            var list = TestData.testTaskList;

            foreach (var delta in TestData.Deltas) {

                // update & remove
                for (int index = 0; index < list.Size; index++) {
                    if (delta.Retained.Contains(list.Tasks[index].ID)) {
                        // update code would be here
                    } else {
                        list.RemoveAt(index);
                    }
                }

                // add new tasks
                for (int i = 0; i < delta.Added.Length; i++) {
                    list.Add(new TestStruct { ID = delta.Added[i] });
                }

            }
        }

        [Benchmark]
        public void TaskList_Optimised_SimulatedCamMoves() {

            var list = TestData.testTaskListOptimised;

            foreach (var delta in TestData.Deltas) {

                // update & remove
                for (int index = 0; index < list.Size; index++) {
                    if (delta.Retained.Contains(list.Tasks[index].ID)) {
                        // update code would be here
                    } else {
                        list.RemoveAt(index);
                    }
                }

                list.PrepareToAdd(delta.Added.Length);

                // add new tasks
                for (int i = 0; i < delta.Added.Length; i++) {
                    ref var task = ref list.Tasks[list.NextUsableTaskIndex];

                    task.ID = delta.Added[i];
                    task.IsActive = true;
                }

            }
        }

        [Benchmark]
        public void FastList_SimulatedCamMoves() {

            var list = TestData.testFastList;

            foreach (var delta in TestData.Deltas) {

                // update & remove
                for (int index = list.m_size; index-- > 0;) {
                    if (delta.Retained.Contains(list.m_buffer[index].ID)) {
                        // update code would be here
                    } else {
                        list.RemoveAt(index);
                    }
                }

                // add new tasks
                for (int i = 0; i < delta.Added.Length; i++) {
                    list.Add(new TestStruct { ID = delta.Added[i], IsActive = true, });
                }

            }
        }

        internal struct TestStruct {
            public int ID;
            public bool IsActive;
        }

        internal struct Delta {
            public HashSet<int> Retained;
            public int[] Added;
        }

        internal static class TestData {
            private static int[] rawData;

            internal static FastList<TestStruct> testFastList;

            internal static TaskList<TestStruct> testTaskList;

            internal static TaskList<TestStruct> testTaskListOptimised;

            internal static int Center;
            internal static int Zoom;

            internal static List<Delta> Deltas;

            internal static void InitialiseLists() {
                // create test lists
                testFastList = new FastList<TestStruct>();
                testTaskList = new TaskList<TestStruct>();
                testTaskListOptimised = new TaskList<TestStruct>();

                // delegates
                testTaskList.OnAddTask = OnAddTL;
                testTaskList.OnRemoveTask = OnDelTL;
                testTaskList.TaskIsActive = IsActiveTL;

                testTaskListOptimised.OnAddTask = OnAddTLO;
                testTaskListOptimised.OnRemoveTask = OnDelTLO;
                testTaskListOptimised.TaskIsActive = IsActiveTLO;

                // populate raw data with unique values
                rawData = new int[36864u];
                for (int i = 0; i < rawData.Length; i++) {
                    rawData[i] = i;
                }
            }

            internal static void GenerateDeltas() {
                Center = rawData.Length / 2;
                Zoom = 500;

                HashSet<int> previous;

                Delta currentViewport = new Delta {
                    Retained = new HashSet<int>(0),
                    Added = rawData.AsSpan(Center - Zoom, Zoom * 2).ToArray(),
                };

                Deltas = new List<Delta>();

                Deltas.Add(currentViewport);

                previous = new HashSet<int>(currentViewport.Added);

                var CameraMoves = new Dictionary<int, int> {
                    { Center -= 250, Zoom }, // move left half screen
                    { Center -= 1, Zoom += 500 }, // zoom out
                    { Center -= 750, Zoom }, // move left full screen
                    { Center += 2, Zoom -= 200 }, // zoom in
                    { Center += 2000, Zoom += 1000 }, // move right, zoom out
                };

                var added = new List<int>();
                var retained = new List<int>();

                foreach (var move in CameraMoves) {
                    var viewport = rawData.AsSpan(move.Key - move.Value, move.Value * 2).ToArray();

                    retained.Clear();
                    added.Clear();

                    foreach (var item in viewport) {
                        if (previous.Contains(item)) {
                            retained.Add(item);
                        } else {
                            added.Add(item);
                        }
                    }

                    previous = new HashSet<int>(viewport);

                    currentViewport = new Delta {
                        Retained = new HashSet<int>(retained.ToArray()),
                        Added = added.ToArray(),
                    };

                    Deltas.Add(currentViewport);
                }
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

            private static void OnAddTLO(int index) {
                testTaskListOptimised.Tasks[index].IsActive = true;
            }
            private static void OnDelTLO(int index) {
                testTaskListOptimised.Tasks[index].IsActive = false;
            }
            private static bool IsActiveTLO(int index) {
                return testTaskListOptimised.Tasks[index].IsActive;
            }
        }
    }
}
