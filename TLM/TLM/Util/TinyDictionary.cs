using CSUtil.Commons;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.Util {
	/// <summary>
	/// Dictionary for use cases with a small number of entries and arbitrary keys
	/// </summary>
	/// <typeparam name="TKey">key type</typeparam>
	/// <typeparam name="TValue">value type</typeparam>
	public class TinyDictionary<TKey, TValue> : IDictionary<TKey, TValue> {
		private TKey[] keys;
		private TValue[] values;
		private KeyValuePair<TKey, TValue>[] keyValuePairs;

		public TinyDictionary() {
			Clear();
		}

		public ICollection<TKey> Keys {
			get {
				return keys.ToList();
			}
		}

		public ICollection<TValue> Values {
			get {
				return values.ToList();
			}
		}

		public int Count {
			get {
				return values.Length;
			}
		}

		public bool IsReadOnly {
			get {
				return false;
			}
		}

		public override string ToString() {
			return this.DictionaryToString();
		}

		public TValue this[TKey key] {
			get {
				if (key == null) {
					throw new ArgumentNullException();
				}

				int keyIndex = IndexOfKey(key);
				if (keyIndex < 0) {
					throw new KeyNotFoundException($"Key '{key}' not found in dictionary: {string.Join(", ", keyValuePairs.Select(x => x.Key.ToString() + " => " + ToStringExt.ToString(x.Value)).ToArray())}");
				}

				return values[keyIndex];
			}

			set {
				if (key == null) {
					throw new ArgumentNullException();
				}

				Add(key, value);
			}
		}

		public bool ContainsKey(TKey key) {
			return IndexOfKey(key) >= 0;
		}

		public void Add(TKey key, TValue value) {
			int keyIndex = IndexOfKey(key);
			if (keyIndex >= 0) {
				values[keyIndex] = value;
				keyValuePairs[keyIndex] = new KeyValuePair<TKey, TValue>(key, value);
			} else {
				int len = Count;
				int newLen = len + 1;

				TKey[] newKeys = new TKey[newLen];
				TValue[] newValues = new TValue[newLen];
				KeyValuePair<TKey, TValue>[] newKeyValuePairs = new KeyValuePair<TKey, TValue>[newLen];

				Array.Copy(keys, newKeys, len);
				Array.Copy(values, newValues, len);
				Array.Copy(keyValuePairs, newKeyValuePairs, len);

				newKeys[len] = key;
				newValues[len] = value;
				newKeyValuePairs[len] = new KeyValuePair<TKey, TValue>(key, value);

				keys = newKeys;
				values = newValues;
				keyValuePairs = newKeyValuePairs;
			}
		}

		public bool Remove(TKey key) {
			int keyIndex = IndexOfKey(key);
			if (keyIndex < 0) {
				return false;
			}

			int len = Count;
			int newLen = len - 1;

			TKey[] newKeys = new TKey[newLen];
			TValue[] newValues = new TValue[newLen];
			KeyValuePair<TKey, TValue>[] newKeyValuePairs = new KeyValuePair<TKey, TValue>[newLen];

			if (keyIndex > 0) {
				// copy 0..keyIndex-1
				Array.Copy(keys, 0, newKeys, 0, keyIndex);
				Array.Copy(values, 0, newValues, 0, keyIndex);
				Array.Copy(keyValuePairs, 0, newKeyValuePairs, 0, keyIndex);
			}

			if (keyIndex < newLen) {
				// copy keyIndex+1..newLen-1
				int remLen = newLen - keyIndex;
				Array.Copy(keys, keyIndex + 1, newKeys, keyIndex, remLen);
				Array.Copy(values, keyIndex + 1, newValues, keyIndex, remLen);
				Array.Copy(keyValuePairs, keyIndex + 1, newKeyValuePairs, keyIndex, remLen);
			}
			
			keys = newKeys;
			values = newValues;
			keyValuePairs = newKeyValuePairs;

			return true;
		}

		public bool TryGetValue(TKey key, out TValue value) {
			int keyIndex = IndexOfKey(key);
			if (keyIndex < 0) {
				value = default(TValue);
				return false;
			}

			value = values[keyIndex];
			return true;
		}

		public void Add(KeyValuePair<TKey, TValue> item) {
			Add(item.Key, item.Value);
		}

		public void Clear() {
			keys = new TKey[0];
			values = new TValue[0];
			keyValuePairs = new KeyValuePair<TKey, TValue>[0];
		}

		public bool Contains(KeyValuePair<TKey, TValue> item) {
			return ContainsKey(item.Key);
		}

		public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
			keyValuePairs.CopyTo(array, arrayIndex);
		}

		public bool Remove(KeyValuePair<TKey, TValue> item) {
			return Remove(item.Key);
		}

		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
			return new TinyDictionaryEnumerator<TKey, TValue>(this);
		}

		IEnumerator IEnumerable.GetEnumerator() {
			return new TinyDictionaryEnumerator<TKey, TValue>(this);
		}

		protected int IndexOfKey(TKey key) {
			if (key == null) {
				return -1;
			}

			for (int i = 0; i < keys.Length; ++i) {
				if (key.Equals(keys[i])) {
					return i;
				}
			}
			return -1;
		}

		protected class TinyDictionaryEnumerator<TKey, TValue> : IEnumerator<KeyValuePair<TKey, TValue>> {
			private int currentIndex = -1;
			private TinyDictionary<TKey, TValue> dict;

			public TinyDictionaryEnumerator(TinyDictionary<TKey, TValue> dict) {
				this.dict = dict;
			}

			public KeyValuePair<TKey, TValue> Current {
				get {
					return dict.keyValuePairs[currentIndex];
				}
			}

			object IEnumerator.Current {
				get {
					return dict.keyValuePairs[currentIndex];
				}
			}

			public void Dispose() {
				dict = null;
			}

			public bool MoveNext() {
				return ++currentIndex < dict.Count;
			}

			public void Reset() {
				currentIndex = -1;
			}
		}
	}
}
