namespace TrafficManager.Util {

    /// <summary>
    /// <para>
    /// This list is designed specifically for persistent render tasks with
    /// intermittent bulk additions/removals/updates due to camera move or
    /// tool state change.
    /// </para>
    /// <para>
    /// It uses a sparse array to store tasks - any iterations must take that
    /// in to account, eg. by checking elements with <see cref="TaskIsActive"/>
    /// delegate. It is up to calling code to determine how tasks are marked
    /// active/inactive via three mandatory delegates.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The type of render task to store.</typeparam>
    /// <remarks><i>Delegates must be assigned prior to use!</i></remarks>
    public class SparseTaskList<T> {

        /// <summary>
        /// When arrays are initialsied or their capacity needs increasing,
        /// they will be sized in multiples of this value. Must be Power of 2.
        /// </summary>
        public const int BLOCK_SIZE = 64;

        /// <summary>
        /// Delegate for mandatory <see cref="OnAfterAddTask"/> and
        /// <see cref="OnAfterRemoveTask"/> event handlers.
        /// </summary>
        /// <param name="index">
        /// The index of the item in the <see cref="Tasks"/> array.
        /// Guaranteed to exist.
        /// </param>
        /// <remarks>
        /// The event handlers must at least mark items active or inactive,
        /// as applicable to the event.
        /// </remarks>
        public delegate void SparseListEvent(int index);

        /// <summary>
        /// Delegate for mandatory <see cref="TaskIsActive"/> method which is
        /// used to test wheter an item in the <see cref="Tasks"/> array is
        /// marked as active.
        /// </summary>
        /// <param name="index">
        /// The index of the item in the <see cref="Tasks"/> array.
        /// Guaranteed to exist when invoked by <see cref="SparseTaskList{T}"/>.
        /// </param>
        /// <returns>
        /// Return <c>true</c> if the item is active, othewise <c>false</c>.
        /// </returns>
        public delegate bool SparseListItemActive(int index);

        /// <summary>
        /// The block size by which arrays will be extended when neccessary.
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="BLOCK_SIZE"/> can be modified via ctor.
        /// </remarks>
        public int BlockSize { get; private set; }

        /// <summary>
        /// Sparse array for storing tasks.
        /// </summary>
        /// <remarks>
        /// Similar to <c>FastList.m_buffer</c>.
        /// </remarks>
        public T[] Tasks { get; private set; }

        /// <summary>
        /// Current number of used elements, <i>inclusive of gaps</i>, in
        /// the sparse <see cref="Tasks"/> array.
        /// </summary>
        /// <remarks>
        /// Similar to <c>FastList.m_size</c>.
        /// </remarks>
        public int Size { get; private set; }

        /// <summary>
        /// Contains indexes of empty gaps in the <see cref="Tasks"/> array.
        /// </summary>
        private int[] gaps;

        /// <summary>
        /// Number of gaps listed in the <see cref="gaps"/> array.
        /// </summary>
        private int numGaps;

        /// <summary>
        /// Mandatory. Called after an item is added to the <see cref="Tasks"/>
        /// array at specified <c>index</c>.
        /// </summary>
        public SparseListEvent OnAfterAddTask;

        /// <summary>
        /// Mandatory. Called after an item is removed from the <see cref="Tasks"/>
        /// array at specified <c>index</c>.
        /// </summary>
        public SparseListEvent OnAfterRemoveTask;

        /// <summary>
        /// Mandatory. Invoked when <see cref="SparseTaskList{T}"/> needs to
        /// check whether a task at specified <c>index</c> is active.
        /// </summary>
        public SparseListItemActive TaskIsActive;

        /// <summary>
        /// Initializes a new instance of the <see cref="SparseTaskList{T}"/> class.
        /// </summary>
        /// <param name="blockSize">
        /// Optional: Specify custom block size. Defaults to <see cref="BLOCK_SIZE"/>.
        /// Must be Power of 2.
        /// </param>
        public SparseTaskList(int blockSize = BLOCK_SIZE) {
            Shortcuts.AssertPowerOf2(blockSize);

            BlockSize = blockSize;
        }

        /// <summary>
        /// Returns the unused capacity of the <see cref="gaps"/> array.
        /// </summary>
        public int GapsCapacity => gaps != null
            ? gaps.Length - numGaps
            : 0;

        /// <summary>
        /// Returns the unused capacity of the <see cref="Tasks"/> array,
        /// <i>inclusive of gaps</i>.
        /// </summary>
        public int TasksCapacity => Tasks != null
            ? Tasks.Length - Size + numGaps
            : 0;

        /// <summary>
        /// Call before adding multiple items to ensure there is enough
        /// space in the <see cref="Tasks"/> array.
        /// </summary>
        /// <param name="numItems">
        /// The number of items that will be added.
        /// </param>
        public void PrepareToAdd(int numItems) {
            if (numItems <= 0 || TasksCapacity >= numItems) {
                return;
            }

            if (Tasks == null) {
                Tasks = new T[SnapToBlock(numItems)];
                return;
            }

            int capacity = Tasks.Length + numItems - numGaps;
            T[] replacement = new T[SnapToBlock(capacity)];

            for (int i = 0; i < Size; i++) {
                replacement[i] = Tasks[i];
            }

            Tasks = replacement;
        }

        /// <summary>
        /// Obtain index of next gap in the <see cref="Tasks"/> array.
        /// </summary>
        /// <remarks>
        /// <i>Assumes that the index will be used</i>.
        /// </remarks>
        public int NextUsableTaskIndex => (numGaps > 0)
            ? gaps[--numGaps]
            : Size++;

        /// <summary>
        /// Adds an item to the <see cref="Tasks"/> array.
        /// </summary>
        /// <param name="item">
        /// The item to add.
        /// </param>
        /// <param name="prePrepared">
        /// If you invoke <see cref="PrepareToAdd(int)"/> prior to
        /// adding a batch of items, and you are certain you prepared
        /// enough space for those items, set this <c>true</c> to bypass
        /// a chunk of internal code and get maximal performance.
        /// </param>
        /// <remarks>
        /// It us up to external code to update the item at the specified
        /// <paramref name="index"/> in such a way that it can be
        /// identified as "active" element in subsequent iterations. To
        /// achieve this, use the <see cref="OnAfterAddTask"/> event.
        /// </remarks>
        public void Add(T item, bool prePrepared = false) {
            if (!prePrepared && TasksCapacity == 0) {
                PrepareToAdd(1);
            }

            int index = NextUsableTaskIndex;
            Tasks[index] = item;
            OnAfterAddTask(index);
        }

        /// <summary>
        /// Call before removing multiple items to ensure there is
        /// enough space in the <see cref="gaps"/> array.
        /// </summary>
        /// <param name="numItems">
        /// The number of items that will be removed.
        /// </param>
        public void PrepareToRemove(int numItems) {
            if (numItems <= 0 || GapsCapacity >= numItems) {
                return;
            }

            if (gaps == null) {
                gaps = new int[SnapToBlock(numItems)];
                return;
            }

            int capacity = numGaps + numItems;
            int[] replacement = new int[SnapToBlock(capacity)];

            for (int i = 0; i < numGaps; i++) {
                replacement[i] = gaps[i];
            }

            gaps = replacement;
        }

        /// <summary>
        /// "Removes" item at specific index. The item is not actually
        /// removed from the <see cref="Tasks"/> array; it's index is
        /// added to the <see cref="gaps"/> array so it can be filled
        /// later when adding new items.
        /// </summary>
        /// <param name="index">The index of the item to remove</param>
        /// <param name="prePrepared">
        /// If you invoke <see cref="PrepareToRemove(int)"/> prior to
        /// removing a batch of items, and you are certain you prepared
        /// enough gaps for those items, set this <c>true</c> to bypass
        /// a chunk of internal code and get maximal performance.
        /// </param>
        /// <remarks>
        /// It us up to external code to update the item at the specified
        /// <paramref name="index"/> in such a way that it can be
        /// identified as "vacant" element in subsequent iterations. To
        /// achieve this, use the <see cref="OnAfterRemoveTask"/> event.
        /// </remarks>
        public void RemoveAt(int index, bool prePrepared = false) {
            if (index >= Size || !TaskIsActive(index)) {
                return;
            }

            if (!prePrepared) {

                // Edge-case: Removing item at end of used portion of Tasks array.
                if (index == Size - 1) {
                    --Size;
                    OnAfterRemoveTask(index);
                    return;
                }

                if (GapsCapacity == 0) {

                    // Edge-case: Removing last remaining active item in Tasks array.
                    if (numGaps == (Size - 1)) {
                        Clear(skipEvents: true);
                        OnAfterRemoveTask(index);
                        return;
                    }

                    PrepareToRemove(1);
                }
            }

            gaps[numGaps++] = index;
            OnAfterRemoveTask(index);
        }

        /// <summary>
        /// "Clear" the arrays but retain their capacity.
        /// </summary>
        /// <param name="skipEvents">Internal use only.</param>
        public void Clear(bool skipEvents = false) {
            if (!skipEvents) {
                for (int index = 0; index < Size; index++) {
                    if (TaskIsActive(index)) {
                        OnAfterRemoveTask(index);
                    }
                }
            }
            Size = 0;
            numGaps = 0;
        }

        /// <summary>
        /// Releases the arrays to free memory.
        /// </summary>
        /// <param name="skipEvents">
        /// If <c>true</c>, removal of any remaining active tasks will
        /// not trigger the <see cref="OnAfterRemoveTask"/> event.
        /// </param>
        public void Release(bool skipEvents = false) {
            Clear(skipEvents);
            Tasks = null;
            gaps = null;
        }

        /// <summary>
        /// Steps <paramref name="num"/> up to the nearest multiple of
        /// <see cref="BlockSize"/> (similar to how slider step works).
        /// </summary>
        /// <param name="num">The number.</param>
        /// <returns>The nearest <see cref="BlockSize"/> step.</returns>
        /// <remarks>
        /// <para>
        /// If <paramref name="num"/> is <c>0</c>, it will return <c>0</c>.
        /// </para>
        /// <para>
        /// If <paramref name="num"/> is <c>1</c>, it will return
        /// <c>BlockSize</c>.
        /// </para>
        /// <para>
        /// If <paramref name="num"/> is <c>BlockSize+1</c>, it will
        /// return <c>2*BlockSize</c>, etc.
        /// </para>
        /// </remarks>
        private int SnapToBlock(int num) => (num + BlockSize - 1) & -BlockSize;
    }
}
