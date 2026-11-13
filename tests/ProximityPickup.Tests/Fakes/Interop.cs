// Fake IL2CPP interop types and the fake IL2CPP dictionary.

using System.Collections;

namespace Il2CppInterop.Runtime.InteropTypes
{
    public class Il2CppObjectBase
    {
        private static long nextPointer = 0x10000;

        // A test can set it, to make a pointer come back for a new object.
        public IntPtr Pointer { get; set; } = new IntPtr(nextPointer += 8);

        public T TryCast<T>() where T : Il2CppObjectBase
        {
            return this as T;
        }
    }
}

namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    public class Il2CppArrayBase<T> : IEnumerable<T>
    {
        private readonly List<T> items;

        public Il2CppArrayBase(IEnumerable<T> values) { items = new List<T>(values); }

        public int Length => items.Count;

        public IEnumerator<T> GetEnumerator() => items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();
    }
}

namespace Il2CppSystem
{
    public struct ValueTuple<T1, T2> { }
}

namespace Il2CppSystem.Collections.Generic
{
    public class List<T> : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
    {
        private readonly System.Collections.Generic.List<T> items = new System.Collections.Generic.List<T>();

        public int Count => items.Count;
        public T this[int index] => items[index];

        public void Add(T item) { items.Add(item); }
    }

    public class HashSet<T>
    {
        private readonly System.Collections.Generic.HashSet<T> items = new System.Collections.Generic.HashSet<T>();

        public bool Add(T item) => items.Add(item);
        public bool Contains(T item) => items.Contains(item);
    }

    // Keeps the order of Add, and accepts a null key, which a BCL dictionary does not.
    public class Dictionary<TKey, TValue>
    {
        private readonly List<KeyValuePair<TKey, TValue>> entries = new List<KeyValuePair<TKey, TValue>>();

        public void Add(TKey key, TValue value) { entries.Add(new KeyValuePair<TKey, TValue>(key, value)); }

        public Enumerator GetEnumerator() => new Enumerator(entries);

        public sealed class Enumerator
        {
            private readonly List<KeyValuePair<TKey, TValue>> entries;
            private int index = -1;

            public Enumerator(List<KeyValuePair<TKey, TValue>> entries) { this.entries = entries; }

            public KeyValuePair<TKey, TValue> Current => entries[index];
            public bool MoveNext() => ++index < entries.Count;
        }
    }
}
