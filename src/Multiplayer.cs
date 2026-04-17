using StardewModdingAPI.Events;
using StardewValley;
using ichortower.TowerCore;

namespace ichortower.MetamorphicCube;

internal class Multiplayer
{
    [SmapiEvent]
    public static void ModMessageReceived(object sender, ModMessageReceivedEventArgs e)
    {
        if (!e.FromModID.Equals(Main.ModId)) {
            return;
        }
        switch (e.Type) {
        case "CleanUp":
            Transmuter.CleanUp();
            break;
        case "CleanupListener":
            if (Game1.IsMasterGame) {
                Transmuter.AddCleanupListener();
            }
            break;
        case "CreateLocation":
            _ = Embedded.TheLocation;
            break;
        default:
            break;
        }
    }
}
