using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace TrafficManager.Util {
    /// <summary>
    /// A spiral coords generator that will cache all previously generated values,
    /// and only generate new ones if necessary.
    /// </summary>
    public class Spiral {
        private readonly string radiusOutOfRangeMessage = "Must be in the range [1,int.MaxValue]";

        private int _maxRadius;
        private IEnumerator<Vector2> _enumerator;
        private List<Vector2> _spiralCoords;
        private ReadOnlyCollection<Vector2> _spiralCoordsReadOnly;

        /// <summary>
        /// Initializes a new instance of the <see cref="Spiral"/> class.
        /// A spiral coords generator that will cache all previously generated values,
        /// and only generate new ones if necessary.
        /// It will be initialized with a radius of 9.
        /// </summary>
        public Spiral()
            : this(9) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Spiral"/> class.
        /// A spiral coords generator that will cache all previously generated values,
        /// and only generate new ones if necessary.
        /// </summary>
        /// <param name="radius">Radius to start with.</param>
        public Spiral(int radius) {
            if (radius < 1) {
                throw new ArgumentOutOfRangeException(nameof(radius), radiusOutOfRangeMessage);
            }

            _maxRadius = 0;
            _enumerator = LoopUtil.GenerateSpiralGridCoordsClockwise()
                .GetEnumerator();
            _spiralCoords = new List<Vector2>(radius * radius);
            _spiralCoordsReadOnly = new ReadOnlyCollection<Vector2>(_spiralCoords);

            Ensure(radius);
        }

        /// <summary>
        /// Gets the largest radius the object was ever passed.
        /// </summary>
        public int MaxRadius => _maxRadius;

        /// <summary>
        /// Gets spiral coords and ensures that the given radius is satisfied.
        /// </summary>
        /// <param name="radius">Needed radius for the spiral coords.</param>
        /// <returns>
        /// A readonly collection of spiral coords.
        /// The result won't generate a new collection for performance reasons,
        /// meaning it will return a reference to the same mutable underlying collection.
        /// The returned collection will always contain the coords for 'MaxRadius',
        /// the largest radius it was ever passed.
        /// </returns>
        public ReadOnlyCollection<Vector2> GetCoords(int radius) {
            if (radius < 1) {
                throw new ArgumentOutOfRangeException(nameof(radius), radiusOutOfRangeMessage);
            }

            Ensure(radius);
            return _spiralCoordsReadOnly;
        }

        private void Ensure(int radius) {
            if (radius <= _maxRadius) {
                return;
            }

            var enumerateCount = radius * radius;
            _spiralCoords.Capacity = enumerateCount;
            for (int i = _spiralCoords.Count; i < enumerateCount; i++) {
                var hasNext = _enumerator.MoveNext();
                if (hasNext) {
                    _spiralCoords.Add(_enumerator.Current);
                }
            }

            _maxRadius = radius;
        }
    }
}
