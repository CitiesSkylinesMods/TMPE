using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace TrafficManager.Util {
    using ColossalFramework.Math;

    /// <summary>
    /// A spiral coords generator that will cache all previously generated values,
    /// and only generate new ones if necessary.
    /// </summary>
    public class Spiral {
        private readonly string radiusOutOfRangeMessage = "Must be in the range [1,int.MaxValue]";

        private int _maxRadius;
        private IEnumerator<Vector2> _enumeratorClockwise;
        private IEnumerator<Vector2> _enumeratorCounterClockwise;
        private List<Vector2> _spiralCoordsClockwise;
        private List<Vector2> _spiralCoordsCounterclockwise;
        private ReadOnlyCollection<Vector2> _spiralCoordsClockwiseReadOnly;
        private ReadOnlyCollection<Vector2> _spiralCoordsCounterclockwiseReadOnly;

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
            _enumeratorClockwise = LoopUtil.GenerateSpiralGridCoordsClockwise()
                .GetEnumerator();
            _enumeratorCounterClockwise = LoopUtil.GenerateSpiralGridCoordsCounterclockwise()
                .GetEnumerator();
            _spiralCoordsClockwise = new List<Vector2>(radius * radius);
            _spiralCoordsCounterclockwise = new List<Vector2>(radius * radius);
            _spiralCoordsClockwiseReadOnly = new ReadOnlyCollection<Vector2>(_spiralCoordsCounterclockwise);
            _spiralCoordsCounterclockwiseReadOnly = new ReadOnlyCollection<Vector2>(_spiralCoordsCounterclockwise);

            Ensure(radius);
        }

        /// <summary>
        /// Gets the largest radius the object was ever passed.
        /// </summary>
        public int MaxRadius => _maxRadius;

        /// <summary>
        /// Gets spiral in random direction and ensures that the given radius is satisfied
        /// </summary>
        /// <param name="radius">Needed radius for the spiral coords.</param>
        /// <param name="r">Randomizer instance to perform randomization</param>
        /// <returns>
        /// A readonly collection of spiral coords.
        /// The result won't generate a new collection for performance reasons,
        /// meaning it will return a reference to the same mutable underlying collection.
        /// The returned collection will always contain the coords for 'MaxRadius',
        /// the largest radius it was ever passed.
        /// </returns>
        public ReadOnlyCollection<Vector2> GetCoordsRandomDirection(int radius, ref Randomizer r) {
            return r.Int32(2) == 0 ? GetCoordsCounterclockwise(radius) : GetCoordsClockwise(radius);
        }

        /// <summary>
        /// Gets spiral coords clockwise and ensures that the given radius is satisfied.
        /// </summary>
        /// <param name="radius">Needed radius for the spiral coords.</param>
        /// <returns>
        /// A readonly collection of spiral coords.
        /// The result won't generate a new collection for performance reasons,
        /// meaning it will return a reference to the same mutable underlying collection.
        /// The returned collection will always contain the coords for 'MaxRadius',
        /// the largest radius it was ever passed.
        /// </returns>
        public ReadOnlyCollection<Vector2> GetCoordsClockwise(int radius) {
            if (radius < 1) {
                throw new ArgumentOutOfRangeException(nameof(radius), radiusOutOfRangeMessage);
            }

            Ensure(radius);
            return _spiralCoordsClockwiseReadOnly;
        }

        /// <summary>
        /// Gets spiral coords counterclockwise and ensures that the given radius is satisfied.
        /// </summary>
        /// <param name="radius">Needed radius for the spiral coords.</param>
        /// <returns>
        /// A readonly collection of spiral coords.
        /// The result won't generate a new collection for performance reasons,
        /// meaning it will return a reference to the same mutable underlying collection.
        /// The returned collection will always contain the coords for 'MaxRadius',
        /// the largest radius it was ever passed.
        /// </returns>
        public ReadOnlyCollection<Vector2> GetCoordsCounterclockwise(int radius) {
            if (radius < 1) {
                throw new ArgumentOutOfRangeException(nameof(radius), radiusOutOfRangeMessage);
            }

            Ensure(radius);
            return _spiralCoordsCounterclockwiseReadOnly;
        }

        private void Ensure(int radius) {
            if (radius <= _maxRadius) {
                return;
            }

            var enumerateCount = radius * radius;

            _spiralCoordsClockwise.Capacity = enumerateCount;
            for (int i = _spiralCoordsClockwise.Count; i < enumerateCount; i++) {
                var hasNext = _enumeratorClockwise.MoveNext();
                if (hasNext) {
                    _spiralCoordsClockwise.Add(_enumeratorClockwise.Current);
                }
            }

            _spiralCoordsCounterclockwise.Capacity = enumerateCount;
            for (int i = _spiralCoordsCounterclockwise.Count; i < enumerateCount; i++) {
                var hasNext = _enumeratorCounterClockwise.MoveNext();
                if (hasNext) {
                    _spiralCoordsCounterclockwise.Add(_enumeratorCounterClockwise.Current);
                }
            }

            _maxRadius = radius;
        }
    }
}
