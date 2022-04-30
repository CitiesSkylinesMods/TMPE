namespace TrafficManager.Util {

    /// <summary>
    /// This list is designed specifically for persistent render tasks with
    /// intermittent bulk additions/removals/updates due to camera move or
    /// tool state change.
    /// </summary>
    /// <typeparam name="T">The type of render task to store.</typeparam>
    /// <remarks><i>Delegates must be assigned prior to use!</i></remarks>
    public class TaskList<T> {

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
        public delegate void TaskListEvent(int index);

        /// <summary>
        /// Delegate for mandatory <see cref="TaskIsActive"/> method which is
        /// used to test wheter an item in the <see cref="Tasks"/> array is
        /// marked as active.
        /// </summary>
        /// <param name="index">
        /// The index of the item in the <see cref="Tasks"/> array.
        /// Guaranteed to exist when invoked by <see cref="TaskList{T}"/>.
        /// </param>
        /// <returns>
        /// Return <c>true</c> if the item is active, othewise <c>false</c>.
        /// </returns>
        public delegate bool TaskListItemActive(int index);

        /// <summary>
        /// The <see cref="Tasks"/> array will be increased in multiples
        /// of this size when necessary.
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="BLOCK_SIZE"/> can be modified via ctor.
        /// </remarks>
        public int BlockSize { get; private set; }

        /// <summary>
        /// Array for storing tasks.
        /// </summary>
        /// <remarks>
        /// Similar to <c>FastList.m_buffer</c>.
        /// </remarks>
        public T[] Tasks { get; private set; }

        /// <summary>
        /// Current number of used elements in <see cref="Tasks"/> array.
        /// </summary>
        /// <remarks>
        /// Similar to <c>FastList.m_size</c>.
        /// </remarks>
        public int Size { get; private set; }

        /// <summary>
        /// Mandatory. Called after an item is added to the <see cref="Tasks"/>
        /// array at specified <c>index</c>.
        /// </summary>
        public TaskListEvent OnAfterAddTask;

        /// <summary>
        /// Mandatory. Called after an item is removed from the <see cref="Tasks"/>
        /// array at specified <c>index</c>.
        /// </summary>
        public TaskListEvent OnAfterRemoveTask;

        /// <summary>
        /// Mandatory. Invoked when <see cref="TaskList{T}"/> needs to
        /// check whether a task at specified <c>index</c> is active.
        /// </summary>
        public TaskListItemActive TaskIsActive;

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskList{T}"/> class.
        /// </summary>
        /// <param name="blockSize">
        /// Optional: Specify custom block size. Defaults to <see cref="BLOCK_SIZE"/>.
        /// Must be Power of 2.
        /// </param>
        public TaskList(int blockSize = BLOCK_SIZE) {
            Shortcuts.AssertPowerOf2(blockSize);

            BlockSize = blockSize;
        }

        /// <summary>
        /// Returns the unused capacity of the <see cref="Tasks"/> array.
        /// </summary>
        public int TasksCapacity => Tasks != null
            ? Tasks.Length - Size
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

            int capacity = Tasks.Length + numItems;
            T[] replacement = new T[SnapToBlock(capacity)];

            for (int i = 0; i < Size; i++) {
                replacement[i] = Tasks[i];
            }

            Tasks = replacement;
        }

        /// <summary>
        /// Obtain next free index in <see cref="Tasks"/> array. Use this
        /// when manually adding batches of items if you want to skip
        /// overheads associated with <see cref="Add(T, bool)"/> but
        /// make sure you <see cref="PrepareToAdd(int)"/> first to ensure
        /// sufficient capacity.
        /// </summary>
        /// <remarks>
        /// <i>Assumes that the index will be used</i>.
        /// </remarks>
        public int NextUsableTaskIndex => Size++;

        /// <summary>
        /// Adds an item to the <see cref="Tasks"/> array, which is then
        /// initialised by <see cref="OnAfterAddTask"/>.
        /// </summary>
        /// <param name="item">
        /// The item to add.
        /// </param>
        /// <param name="prePrepared">
        /// Set <c>true</c> only if you prepared enough space to add a
        /// batch of items using <see cref="PrepareToAdd(int)"/>.
        /// </param>
        public void Add(T item, bool prePrepared = false) {
            if (!prePrepared && TasksCapacity == 0) {
                PrepareToAdd(1);
            }

            Tasks[Size] = item;
            OnAfterAddTask(Size);

            ++Size;
        }

        /// <summary>
        /// Removes item at specific index by replacing it with last
        /// used element of the <see cref="Tasks"/> array, which is
        /// then cleared via <see cref="OnAfterRemoveTask"/>.
        /// </summary>
        /// <param name="index">The index of the item to remove</param>
        public void RemoveAt(int index) {
            if (index < 0 || index >= Size) {
                return;
            }

            --Size;

            if (index != Size) {
                Tasks[index] = Tasks[Size];
            }

            OnAfterRemoveTask(Size);
        }

        /// <summary>
        /// "Clear" the arrays but retain their capacity.
        /// </summary>
        /// <param name="skipEvents">
        /// If <c>true</c>, removal of any remaining active tasks will
        /// not trigger the <see cref="OnAfterRemoveTask"/> event.
        /// </param>
        public void Clear(bool skipEvents = false) {
            if (!skipEvents) {
                for (int index = 0; index < Size; index++) {
                    OnAfterRemoveTask(index);
                }
            }

            Size = 0;
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
