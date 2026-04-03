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

        helper.Events.Content.AssetReady += Spoof.AssetReady;
        helper.Events.Content.AssetRequested += GuideBook.AssetRequested;
        helper.Events.Content.AssetRequested += Mail.AssetRequested;
        helper.Events.Content.AssetRequested += RecipeData.AssetRequested;
        helper.Events.Content.AssetRequested += Shops.AssetRequested;
        helper.Events.Content.AssetRequested += Transmuter.AssetRequested;
        helper.Events.Content.AssetRequested += Embedded.AssetRequested;
        helper.Events.Content.AssetsInvalidated += RecipeData.AssetsInvalidated;
        helper.Events.GameLoop.GameLaunched += SecretNotes.GameLaunched;
        helper.Events.GameLoop.ReturnedToTitle += RecipeData.ReturnedToTitle;
        helper.Events.GameLoop.ReturnedToTitle += Embedded.ReturnedToTitle;
        helper.Events.GameLoop.DayEnding += Embedded.DayEnding;
        helper.Events.Player.Warped += Transmuter.Warped;
        helper.Events.World.ObjectListChanged += Transmuter.ObjectListChanged;

        GameStateQuery.Register($"{Main.ModId}_COMMUNITY_ROOMS_COMPLETED",
                Mail.gsq_COMMUNITY_ROOMS_COMPLETED);

        #if DEBUG
        helper.ConsoleCommands.Add("spoon", "?????", Embedded.CheatCode);
        #endif
    }
}
