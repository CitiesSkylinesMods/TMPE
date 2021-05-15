namespace CSUtil.Commons {
    using System.Collections.Generic;
    using System.Linq;

    public static class ToStringExt {
        public static string DictionaryToString<TKey, TValue>(this IDictionary<TKey, TValue> element) {
            return string.Join(", ", element.Keys.Select(x => $"{ToString(x)}={ToString(element[x])}").ToArray());
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