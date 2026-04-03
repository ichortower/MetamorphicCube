using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.GameData;
using System.Collections.Generic;
using ichortower.TowerCore;

namespace ichortower.MetamorphicCube;

internal class Mail
{
    // usage: {{ModId}}_COMMUNITY_ROOMS_COMPLETED min [max]
    // returns true if the host has completed between min and (optional) max (inclusive) rooms
    // in the community center. jojamart community upgrades are valid, too
    public static bool gsq_COMMUNITY_ROOMS_COMPLETED(string[] query, GameStateQueryContext context)
    {
        if (!ArgUtility.TryGetInt(query, 1, out int min, out string error)) {
            Log.Error(error);
            return false;
        }
        if (!ArgUtility.TryGetOptionalInt(query, 2, out int max, out error, defaultValue: 7)) {
            Log.Warn(error);
            max = 7;
        }
        string[] mails = new string[] {
            "ccBoilerRoom", "ccBulletin", "ccCraftsRoom", "ccFishTank", "ccPantry", "ccVault",
        };
        int num = 0;
        foreach (string room in mails) {
            if (Game1.MasterPlayer.hasOrWillReceiveMail(room)) {
                ++num;
            }
        }
        if (num < min || num > max) {
            return false;
        }
        return true;
    }

    internal static void AssetRequested(object sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo("Data/Mail")) {
            e.Edit((asset) => {
                var dict = asset.AsDictionary<string, string>();
                Dictionary<string, string> fromFile = Main.Helper.ModContent.Load
                        <Dictionary<string, string>>("data/mail.json");
                foreach (var entry in fromFile) {
                    string newKey = Tokens.Parse(entry.Key);
                    string newValue = Tokens.Parse(entry.Value);
                    dict.Data[newKey] = newValue;
                }
            }, AssetEditPriority.Default);
        }

        if (e.Name.IsEquivalentTo("Data/TriggerActions")) {
            e.Edit((asset) => {
                var list = asset.GetData<List<TriggerActionData>>();
                Dictionary<string, TriggerActionData> fromFile = Main.Helper.ModContent.Load
                        <Dictionary<string, TriggerActionData>>("data/trigger-actions.json");
                foreach (var entry in fromFile) {
                    // obviously for general-purpose you would want to compare the
                    // key for replacement but there is no time, we ball
                    //string newKey = Tokens.Parse(entry.Key);
                    entry.Value.Id = Tokens.Parse(entry.Value.Id);
                    entry.Value.Condition = Tokens.Parse(entry.Value.Condition);
                    entry.Value.Action = Tokens.Parse(entry.Value.Action);
                    list.Add(entry.Value);
                }
            }, AssetEditPriority.Default);
        }
    }
}
