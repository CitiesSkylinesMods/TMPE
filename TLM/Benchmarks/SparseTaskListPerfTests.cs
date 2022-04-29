using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TrafficManager.Util;

namespace Benchmarks {
    public class SparseTaskListPerfTests {

        /*
        [GlobalSetup]
        public void Setup() {
            TestData.InitialiseLists();
            TestData.GenerateDeltas();
        }
        */

        [Benchmark]
        public void SparseTaskList_SimulatedCamMoves() {
            TestData.InitialiseLists();
            TestData.GenerateDeltas();

            var list = TestData.testSparseTaskList;

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
        public void SparseTaskList_Manual_SimulatedCamMoves() {
            TestData.InitialiseLists();
            TestData.GenerateDeltas();

            var list = TestData.testSparseTaskListManual;

            foreach (var delta in TestData.Deltas) {

                list.PrepareToRemove(list.Size - delta.Retained.Count);

                // update & remove
                for (int index = 0; index < list.Size; index++) {
                    if (delta.Retained.Contains(list.Tasks[index].ID)) {
                        // update code would be here
                    } else {
                        list.RemoveAt(index, prePrepared: true);
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
            TestData.InitialiseLists();
            TestData.GenerateDeltas();

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

            internal static int[] randomisedData;

            internal static FastList<TestStruct> testFastList;

            internal static SparseTaskList<TestStruct> testSparseTaskList;

            internal static SparseTaskList<TestStruct> testSparseTaskListManual;

            internal static int Center;
            internal static int Zoom;

            internal static List<Delta> Deltas;

            internal static void InitialiseLists() {
                // create test lists
                testFastList = new FastList<TestStruct>();
                testSparseTaskList = new SparseTaskList<TestStruct>();
                testSparseTaskListManual = new SparseTaskList<TestStruct>();

                // delegates
                testSparseTaskList.OnAfterAddTask = OnAddSTL;
                testSparseTaskList.OnAfterRemoveTask = OnDelSTL;
                testSparseTaskList.TaskIsActive = IsActiveSTL;

                testSparseTaskListManual.OnAfterAddTask = OnAddSTLM;
                testSparseTaskListManual.OnAfterRemoveTask = OnDelSTLM;
                testSparseTaskListManual.TaskIsActive = IsActiveSTLM;

                // populate raw data with unique values
                rawData = new int[36864u];
                for (int i = 0; i < rawData.Length; i++) {
                    rawData[i] = i;
                }

                // randomise rawData -> randomisedData
                Random rnd = new Random();
                randomisedData = rawData.OrderBy(x => rnd.Next()).ToArray();
            }

            internal static void GenerateDeltas() {
                Center = randomisedData.Length / 2;
                Zoom = 500;

                HashSet<int> previous;

                Delta currentViewport = new Delta {
                    Retained = new HashSet<int>(0),
                    Added = randomisedData.AsSpan(Center - Zoom, Zoom * 2).ToArray(),
                };

                Deltas = new List<Delta>();

                Deltas.Add(currentViewport);

                previous = new HashSet<int>(currentViewport.Added);

                var CameraMoves = new Dictionary<int, int> {
                    { Center -= 250, Zoom }, // move left half screen
                    { Center -= 1, Zoom += 500 }, // zoom out
                    { Center -= 750, Zoom }, // move left full screen
                    { Center += 1, Zoom -= 200 }, // zoom in
                    { Center += 2000, Zoom += 1000 }, // move right, zoom out
                };

                var added = new FastList<int>();
                var retained = new FastList<int>();

                foreach (var move in CameraMoves) {
                    var viewport = randomisedData.AsSpan(move.Key - move.Value, move.Value * 2).ToArray();

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

            internal static void OnAddSTL(int index) {
                testSparseTaskList.Tasks[index].IsActive = true;
            }
            internal static void OnDelSTL(int index) {
                testSparseTaskList.Tasks[index].IsActive = false;
            }
            internal static bool IsActiveSTL(int index) {
                return testSparseTaskList.Tasks[index].IsActive;
            }

            private static void OnAddSTLM(int index) {
                testSparseTaskListManual.Tasks[index].IsActive = true;
            }
            private static void OnDelSTLM(int index) {
                testSparseTaskListManual.Tasks[index].IsActive = false;
            }
            private static bool IsActiveSTLM(int index) {
                return testSparseTaskListManual.Tasks[index].IsActive;
            }
        }
    }
}
