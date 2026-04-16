using StardewModdingAPI.Events;
using StardewValley.GameData;
using StardewValley.GameData.Shops;
using System.Collections.Generic;
using ichortower.TowerCore;

namespace ichortower.MetamorphicCube;

internal class Shops
{
    [SmapiEvent]
    internal static void AssetRequested(object sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo("Data/Shops")) {
            e.Edit((asset) => {
                var dict = asset.AsDictionary<string, ShopData>();
                if (!dict.Data.TryGetValue("DesertFestival_EggShop", out ShopData shop)) {
                    return;
                }
                shop.Items.Add(new ShopItemData() {
                    TradeItemId = "CalicoEgg",
                    TradeItemAmount = 50,
                    AvailableStock = 1,
                    AvailableStockLimit = LimitedStockMode.Player,
                    Condition = $"!PLAYER_HAS_CRAFTING_RECIPE Current {Transmuter.ItemId}",
                    Id = Transmuter.ItemId,
                    ItemId = Transmuter.ItemId,
                    IsRecipe = true,
                });
                // done?
            });
        }
        if (e.Name.IsEquivalentTo("Data/LostItemsShop")) {
            e.Edit((asset) => {
                var list = asset.GetData<List<LostItem>>();
                LostItem guidebook = new() {
                    Id = $"{Main.ModId}_LIS_Guidebook",
                    ItemId = GuideBook.QualifiedItemId,
                    RequireMailReceived = $"{Main.ModId}_MailFollowup",
                };
                list.Add(guidebook);
            });
        }
    }
}
