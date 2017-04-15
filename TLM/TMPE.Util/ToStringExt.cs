using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Util {
	public static class ToStringExt {
		public static string DictionaryToString<K, V>(this IDictionary<K, V> element) {
			return string.Join(", ", element.Select((x, y) => $"{ToString(x)}={ToString(y)}").ToArray());
		}

		public static string CollectionToString<T>(this ICollection<T> elements) {
			return string.Join(", ", elements.Select(x => ToString(x)).ToArray());
		}

		public static string ArrayToString<T>(this T[] elements) {
			return string.Join(", ", elements.Select(x => ToString(x)).ToArray());
		}

		public static string ToString(object obj) {
			return obj == null ? "<null>" : obj.ToString();
		}
	}
}
