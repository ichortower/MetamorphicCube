using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.Menus;
using System;
using System.IO;

using ichortower.TowerCore;

namespace ichortower.MetamorphicCube;

internal static class Extensions
{
    internal static byte[] ReadAllBytes(this Stream source)
    {
        long origPos = source.Position;
        source.Position = 0;
        MemoryStream ms = new();
        source.CopyTo(ms);
        source.Position = origPos;
        return ms.ToArray();
    }

    internal static Item Clone(this Item src)
    {
        Item clone = src.getOne();
        clone.Stack = src.Stack;
        return clone;
    }

    internal static void ReplaceWithClonesFrom(this Inventory inv, Inventory other)
    {
        if (System.Object.ReferenceEquals(inv, other)) {
            return;
        }
        inv.Clear();
        foreach (Item item in other) {
            inv.Add(item.Clone());
        }
    }

    internal static float Magnitude(this Vector2 vec)
    {
        return Vector2.Distance(vec, Vector2.Zero);
    }

    // equivalent to doing Normalize and then multiplying by scale
    internal static Vector2 SetMagnitude(this Vector2 vec, float scale)
    {
        if (vec == Vector2.Zero) {
            return vec;
        }
        float factor = scale / MathF.Sqrt(vec.X * vec.X + vec.Y * vec.Y);
        return vec * factor;
    }

    internal static Vector2 ClampMagnitude(this Vector2 vec, float min, float max)
    {
        float val = Math.Max(min, Math.Min(vec.Magnitude(), max));
        return vec.SetMagnitude(val);
    }

    internal static Item AddWithStacking(this Inventory inv, Item item)
    {
        item.resetState();
        inv.RemoveEmptySlots();
        for (int i = 0; i < inv.Count; ++i) {
            if (inv[i] is not null && inv[i].canStackWith(item)) {
                int toRemove = item.Stack - inv[i].addToStack(item);
                if (item.ConsumeStack(toRemove) is null) {
                    return null;
                }
            }
        }
        if (inv.Count < Transmuter.Capacity) {
            inv.Add(item);
            return null;
        }
        return item;
    }

    internal static int IncrementGateKeeperValue(this GameLocation loc)
    {
        if (!loc.Name.Equals(Embedded.Strings["Location.Id"])) {
            return 0;
        }
        int cur = 0;
        string key = Embedded.Strings["Location.Gate.Key"];
        if (loc.modData.TryGetValue(key, out string v)) {
            _ = Int32.TryParse(v, out cur);
        }
        ++cur;
        loc.modData[key] = cur.ToString();
        return cur;
    }

    internal static void TryOpenInteriorGate(this GameLocation loc)
    {
        if (loc.IncrementGateKeeperValue() != 4) {
            return;
        }
        DelayedAction.functionAfterDelay(() => {
            Embedded.RemoveStartingGate(loc);
            Main.Helper.Multiplayer.SendMessage(0, "RemoveStartingGate", modIDs: new[]{Main.ModId});
        }, 2000);
    }
}
