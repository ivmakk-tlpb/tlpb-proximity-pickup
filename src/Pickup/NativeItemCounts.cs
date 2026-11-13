using System;
using System.Runtime.InteropServices;
using ItemCounts = Il2CppSystem.Collections.Generic.List<Il2CppSystem.ValueTuple<int, int>>;

namespace ProximityPickup.Pickup
{
    // The item counts of a dig or gather result: a List<ValueTuple<int, int>> of (item id, count) pairs. The interop
    // cannot read ValueTuple items, so this code reads and writes the list in native memory at offsets from the game
    // context.
    internal static class NativeItemCounts
    {
        // An IL2CPP List<ValueTuple<int, int>> and its array of (item id, count) pairs.
        private const int ListItems = 0x10, ListSize = 0x18, ArrayLength = 0x18, ArrayFirst = 0x20, EntrySize = 8;

        private static IntPtr ArrayOf(ItemCounts list) => Marshal.ReadIntPtr(list.Pointer + ListItems);

        internal static int Capacity(ItemCounts list) => Marshal.ReadInt32(ArrayOf(list) + ArrayLength);

        private static int SizeOf(ItemCounts list) => Marshal.ReadInt32(list.Pointer + ListSize);

        // Adds each count to the entry of its item in the target. An item with no entry and no room in the target
        // stays out of the message, and it is in the inventory all the same.
        internal static void AddCounts(ItemCounts target, ItemCounts source)
        {
            IntPtr targetArray = ArrayOf(target), sourceArray = ArrayOf(source);
            for (int i = 0; i < SizeOf(source); i++)
            {
                IntPtr entry = sourceArray + ArrayFirst + i * EntrySize;
                int item = Marshal.ReadInt32(entry), count = Marshal.ReadInt32(entry + 4);
                int index = 0, size = SizeOf(target);
                while (index < size && Marshal.ReadInt32(targetArray + ArrayFirst + index * EntrySize) != item) index++;
                if (index == Capacity(target)) continue;

                IntPtr destination = targetArray + ArrayFirst + index * EntrySize;
                if (index == size)
                {
                    Marshal.WriteInt32(destination, item);
                    Marshal.WriteInt32(destination + 4, 0);
                    Marshal.WriteInt32(target.Pointer + ListSize, size + 1);
                }
                Marshal.WriteInt32(destination + 4, Marshal.ReadInt32(destination + 4) + count);
            }
        }

        internal static int Sum(ItemCounts list)
        {
            int total = 0;
            for (int i = 0; i < SizeOf(list); i++) total += Marshal.ReadInt32(ArrayOf(list) + ArrayFirst + i * EntrySize + 4);
            return total;
        }
    }
}
