using StardewModdingAPI.Events;
using System.Linq;
using System.Reflection;
using ichortower.TowerCore;

namespace ichortower.MetamorphicCube;

internal class Spoof
{
    internal static string Target = null;

    internal static void SetModName(string name)
    {
        if (name is null || name == "") {
            name = Main.Manifest.Name;
        }
        var mod = StardewModdingAPI.Framework.SCore.Instance.ModRegistry.Mods
                .Where(mod => mod.Manifest.UniqueID == Main.ModId).First();
        // please look away, nothing to see here
        FieldInfo dn = mod.GetType().GetField("<DisplayName>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
        dn.SetValue(mod, name);
    }

    internal static void AssetReady(object sender, AssetReadyEventArgs e)
    {
        if (Target is not null && e.Name.IsEquivalentTo(Target)) {
            SetModName(Main.Manifest.Name);
            Target = null;
        }
    }
}
