using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Trade.Utils
{
    /// <summary>
    /// Zero-copy range view over price arrays for .NET Framework 4.7.2
    /// Optimized for indicator calculations without array copying
    /// Provides Span-like behavior for efficient memory usage
    /// </summary>
    public readonly struct PriceRange : IEnumerable<double>
    {
        private readonly double[] _array;
        private readonly int _start;
        private readonly int _length;

        /// <summary>
        /// Create a PriceRange over a portion of an array
        /// </summary>
        /// <param name="array">Source array</param>
        /// <param name="start">Starting index (inclusive)</param>
        /// <param name="length">Number of elements to include</param>
        public PriceRange(double[] array, int start, int length)
        {
            _array = array ?? throw new ArgumentNullException(nameof(array));
            _start = Math.Max(0, start);
            _length = Math.Min(length, array.Length - _start);
            if (_length < 0) _length = 0;
        }

        /// <summary>
        /// Create a PriceRange over entire array
        /// </summary>
        /// <param name="array">Source array</param>
        public PriceRange(double[] array) : this(array, 0, array?.Length ?? 0)
        {
        }

        /// <summary>
        /// Number of elements in this range
        /// </summary>
        public int Length => _length;

        /// <summary>
        /// Check if this range is empty
        /// </summary>
        public bool IsEmpty => _length == 0;

        /// <summary>
        /// Access element by index (zero-copy)
        /// </summary>
        /// <param name="index">Index within the range</param>
        /// <returns>Value at the specified index</returns>
        public double this[int index]
        {
            get
            {
                if (index < 0 || index >= _length)
                    throw new IndexOutOfRangeException($"Index {index} is out of range [0, {_length})");
                return _array[_start + index];
            }
        }

        /// <summary>
        /// Get the first element (throws if empty)
        /// </summary>
        public double First
        {
            get
            {
                if (_length == 0) throw new InvalidOperationException("Range is empty");
                return _array[_start];
            }
        }

        /// <summary>
        /// Get the last element (throws if empty)
        /// </summary>
        public double Last
        {
            get
            {
                if (_length == 0) throw new InvalidOperationException("Range is empty");
                return _array[_start + _length - 1];
            }
        }

        /// <summary>
        /// Implicit conversion from array to PriceRange
        /// </summary>
        /// <param name="array">Source array</param>
        public static implicit operator PriceRange(double[] array) => new PriceRange(array);

        /// <summary>
        /// Implicit conversion from PriceRange to array
        /// </summary>
        /// <param name="priceRange">Source PriceRange</param>
        public static implicit operator double[](PriceRange priceRange) => priceRange._array;

        /// <summary>
        /// Support foreach enumeration (zero-copy)
        /// </summary>
        /// <returns>Enumerator over the range</returns>
        public IEnumerator<double> GetEnumerator()
        {
            for (int i = 0; i < _length; i++)
                yield return _array[_start + i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Convert to array (creates copy - use sparingly)
        /// </summary>
        /// <returns>New array containing the range data</returns>
        public double[] ToArray()
        {
            if (_length == 0) return new double[0];
            var result = new double[_length];
            Array.Copy(_array, _start, result, 0, _length);
            return result;
        }

        /// <summary>
        /// Create a sub-range by skipping elements from the beginning
        /// </summary>
        /// <param name="count">Number of elements to skip</param>
        /// <returns>New PriceRange starting after the skipped elements</returns>
        public PriceRange Skip(int count)
        {
            if (count <= 0) return this;
            if (count >= _length) return new PriceRange(_array, _start, 0);
            return new PriceRange(_array, _start + count, _length - count);
        }

        /// <summary>
        /// Create a sub-range with a maximum number of elements
        /// </summary>
        /// <param name="count">Maximum number of elements to include</param>
        /// <returns>New PriceRange with at most count elements</returns>
        public PriceRange Take(int count)
        {
            if (count <= 0) return new PriceRange(_array, _start, 0);
            if (count >= _length) return this;
            return new PriceRange(_array, _start, count);
        }

        /// <summary>
        /// Create a slice of this range
        /// </summary>
        /// <param name="start">Starting index within this range</param>
        /// <param name="length">Number of elements to include</param>
        /// <returns>New PriceRange representing the slice</returns>
        public PriceRange Slice(int start, int length)
        {
            if (start < 0) start = 0;
            if (start >= _length) return new PriceRange(_array, _start, 0);
            
            var actualLength = Math.Min(length, _length - start);
            if (actualLength <= 0) return new PriceRange(_array, _start, 0);
            
            return new PriceRange(_array, _start + start, actualLength);
        }

        /// <summary>
        /// Calculate sum of all elements in the range (zero-copy)
        /// </summary>
        /// <returns>Sum of all elements</returns>
        public double Sum()
        {
            double sum = 0.0;
            for (int i = 0; i < _length; i++)
                sum += _array[_start + i];
            return sum;
        }

        /// <summary>
        /// Calculate average of all elements in the range (zero-copy)
        /// </summary>
        /// <returns>Average of all elements</returns>
        public double Average()
        {
            if (_length == 0) throw new InvalidOperationException("Cannot calculate average of empty range");
            return Sum() / _length;
        }

        /// <summary>
        /// Find minimum value in the range (zero-copy)
        /// </summary>
        /// <returns>Minimum value</returns>
        public double Min()
        {
            if (_length == 0) throw new InvalidOperationException("Cannot find minimum of empty range");
            double min = _array[_start];
            for (int i = 1; i < _length; i++)
            {
                var value = _array[_start + i];
                if (value < min) min = value;
            }
            return min;
        }

        /// <summary>
        /// Find maximum value in the range (zero-copy)
        /// </summary>
        /// <returns>Maximum value</returns>
        public double Max()
        {
            if (_length == 0) throw new InvalidOperationException("Cannot find maximum of empty range");
            double max = _array[_start];
            for (int i = 1; i < _length; i++)
            {
                var value = _array[_start + i];
                if (value > max) max = value;
            }
            return max;
        }

        /// <summary>
        /// Check if range contains a specific value
        /// </summary>
        /// <param name="value">Value to search for</param>
        /// <returns>True if value is found in the range</returns>
        public bool Contains(double value)
        {
            for (int i = 0; i < _length; i++)
            {
                if (Math.Abs(_array[_start + i] - value) < 1e-10) return true;
            }
            return false;
        }

        /// <summary>
        /// String representation for debugging
        /// </summary>
        /// <returns>String showing range bounds and sample values</returns>
        public override string ToString()
        {
            if (_length == 0) return "PriceRange[Empty]";
            if (_length == 1) return $"PriceRange[{_array[_start]:F2}]";
            if (_length <= 3)
            {
                var values = new string[_length];
                for (int i = 0; i < _length; i++)
                    values[i] = _array[_start + i].ToString("F2");
                return $"PriceRange[{string.Join(", ", values)}]";
            }
            return $"PriceRange[{_array[_start]:F2}...{_array[_start + _length - 1]:F2}] ({_length} elements)";
        }
    }
}