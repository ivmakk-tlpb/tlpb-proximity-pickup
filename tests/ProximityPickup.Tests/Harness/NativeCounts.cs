using System.Runtime.InteropServices;
using CountList = Il2CppSystem.Collections.Generic.List<Il2CppSystem.ValueTuple<int, int>>;

// A native block with the layout of an IL2CPP List<ValueTuple<int, int>>, because the mod reads the item counts at
// their offsets.
internal static class NativeCounts
{
    private const int Items = 0x10, Size = 0x18, ArrayLength = 0x18, ArrayFirst = 0x20, Entry = 8;
    private const int DefaultCapacity = 4;

    public static CountList Make(params (int Item, int Count)[] counts) => Make(Math.Max(DefaultCapacity, counts.Length), counts);

    // A capacity of 0 is the shared empty array of a list that nothing was added to.
    public static CountList Make(int capacity, params (int Item, int Count)[] counts)
    {
        IntPtr array = Marshal.AllocHGlobal(ArrayFirst + capacity * Entry);
        Marshal.WriteInt32(array + ArrayLength, capacity);
        IntPtr list = Marshal.AllocHGlobal(Size + 8);
        Marshal.WriteIntPtr(list + Items, array);
        Marshal.WriteInt32(list + Size, counts.Length);
        for (int i = 0; i < counts.Length; i++)
        {
            Marshal.WriteInt32(array + ArrayFirst + i * Entry, counts[i].Item);
            Marshal.WriteInt32(array + ArrayFirst + i * Entry + 4, counts[i].Count);
        }
        return new CountList { Pointer = list };
    }

    public static (int Item, int Count)[] Read(CountList list)
    {
        IntPtr array = Marshal.ReadIntPtr(list.Pointer + Items);
        int size = Marshal.ReadInt32(list.Pointer + Size);
        var counts = new (int, int)[size];
        for (int i = 0; i < size; i++)
            counts[i] = (Marshal.ReadInt32(array + ArrayFirst + i * Entry), Marshal.ReadInt32(array + ArrayFirst + i * Entry + 4));
        return counts;
    }
}
