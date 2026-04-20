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
        ichortower.TowerCore.ConsoleCommands.Register(this.GetType(), out int _);

        GameStateQuery.Register($"{Main.ModId}_COMMUNITY_ROOMS_COMPLETED",
                Mail.gsq_COMMUNITY_ROOMS_COMPLETED);

        #if DEBUG
        helper.ConsoleCommands.Add("spoon", "?????", Embedded.CheatCode);
        #endif
    }

    #if DEBUG
    [ConsoleCommand("locs", "log all locations in Game1.locations to console")]
    public static void PrintLocs(string command, string[] args)
    {
        Utility.ForEachLocation((loc) => {
            Log.Debug($"{loc.Name} ('{loc.DisplayName}')");
            return true;
        });
    }
    #endif
}
