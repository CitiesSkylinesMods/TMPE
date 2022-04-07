namespace TrafficManager.Util.Extensions {
    using System;
    using System.Collections.Generic;

    public static class FastListExtensions {

        /// <summary>
        /// Creates an empty array with specified <paramref name="capacity"/>
        /// then replaces the internal <c>m_buffer</c> with that array.
        /// </summary>
        /// <typeparam name="T">Type of list.</typeparam>
        /// <param name="this">List to modify.</param>
        /// <param name="capacity">The capacity to ensure.</param>
        /// <remarks>All existing data is discarded.</remarks>
        public static void ClearAndEnsureCapacity<T>(this FastList<T> @this, int capacity) {
            @this.m_size = 0;
            @this.m_buffer = new T[capacity];
        }

        /// <summary>
        /// Simialr to <c>Array.Pop()</c> in JavaScript, this returns the
        /// last item in the list and reduces the <c>m_size</c> of the list.
        /// </summary>
        /// <typeparam name="T">Type of list.</typeparam>
        /// <param name="this">List to pop.</param>
        /// <returns>Returns the last item in the list.</returns>
        /// <remarks>
        /// DOES NOT CHECK LIST SIZE! Make sure call site does that!
        /// </remarks>
        public static T Pop<T>(this FastList<T> @this) {
            return @this.m_buffer[--@this.m_size];
        }

        public static void MakeRoomFor<T>(this FastList<T> @this, int numItems) =>
            @this.EnsureCapacity(@this.m_size + numItems);

        public static int Count<T>(this FastList<T> @this) =>
            @this.m_size;

        public static void Each<T>(this FastList<T> @this, Action<T> action) {
            for (var i = 0; i < @this.m_size; i++)
                action(@this.m_buffer[i]);
        }
    }
}
