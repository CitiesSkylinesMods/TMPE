using System;
using System.Collections.Concurrent;
using CSModLib.Core;

namespace CSModLib.GameObjects {
    public abstract class ExtPrefabInfo<TExtInfo, TInfo>
            where TInfo : PrefabInfo
            where TExtInfo : ExtPrefabInfo<TExtInfo, TInfo>
            {
        private static readonly ConcurrentDictionary<TInfo, TExtInfo> extInfos;

        static ExtPrefabInfo() {
            extInfos = new ConcurrentDictionary<TInfo, TExtInfo>(ReferenceEqualityComparer<TInfo>.Instance);
        }

        public static TExtInfo GetInstance(TInfo info) {
            return extInfos.GetOrAdd(info, i => (TExtInfo)Activator.CreateInstance(typeof(TExtInfo), i));
        }
    }
}
