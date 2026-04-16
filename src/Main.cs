using StardewModdingAPI;
using StardewValley;
using System.Reflection;

using ichortower.TowerCore;

namespace ichortower.MetamorphicCube;

internal sealed class ModMain : Mod
{
    public override void Entry(IModHelper helper)
    {
        ichortower.TowerCore.Main.Mod = this;
        ichortower.TowerCore.Patches.Apply();
        ichortower.TowerCore.SmapiEvents.Register();

        GameStateQuery.Register($"{Main.ModId}_COMMUNITY_ROOMS_COMPLETED",
                Mail.gsq_COMMUNITY_ROOMS_COMPLETED);

        #if DEBUG
        helper.ConsoleCommands.Add("spoon", "?????", Embedded.CheatCode);
        #endif
    }
}
