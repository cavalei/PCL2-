using System;
using System.Collections.Generic;
internal static class Compatibility {
 public static bool TryAdd<K,V>(this IDictionary<K,V> map,K key,V value) {if(map.ContainsKey(key))return false;map.Add(key,value);return true;}
}

