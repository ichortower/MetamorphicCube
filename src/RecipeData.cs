using StardewModdingAPI.Events;
using StardewValley;
using System.Collections.Generic;
using System.Linq;

using ichortower.TowerCore;

namespace ichortower.MetamorphicCube;

internal class RecipeData
{
    internal static string DataAsset = $"{Main.ModId}/Data/Recipes";

    private static Dictionary<string, Recipe> _Data = null;
    internal static Dictionary<string, Recipe> Data {
        get {
            _Data ??= Game1.content.Load<Dictionary<string, Recipe>>(RecipeData.DataAsset);
            return _Data;
        }
    }

    internal static void ReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
    {
        _Data = null;
    }

    internal static void AssetsInvalidated(object sender, AssetsInvalidatedEventArgs e)
    {
        if (e.Names.Any(name => name.IsEquivalentTo(RecipeData.DataAsset))) {
            _Data = null;
        }
    }

    internal static void AssetRequested(object sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(RecipeData.DataAsset)) {
            e.LoadFrom(() => {
                Dictionary<string, Recipe> fromFile = Main.Helper.ModContent.Load
                        <Dictionary<string, Recipe>>("data/recipes.json");
                Dictionary<string, Recipe> ret = new();
                foreach (var entry in fromFile) {
                    string newKey = Tokens.Parse(entry.Key);
                    entry.Value.Id = Tokens.Parse(entry.Value.Id);
                    entry.Value.Condition = Tokens.Parse(entry.Value.Condition);
                    if (entry.Value.GuideBook is not null) {
                        for (int i = 0; i < entry.Value.GuideBook.Inputs.Count; ++i) {
                            string s = Tokens.Parse(entry.Value.GuideBook.Inputs[i]);
                            entry.Value.GuideBook.Inputs[i] = s;
                        }
                        for (int i = 0; i < entry.Value.GuideBook.Outputs.Count; ++i) {
                            string s = Tokens.Parse(entry.Value.GuideBook.Outputs[i]);
                            entry.Value.GuideBook.Outputs[i] = s;
                        }
                        entry.Value.GuideBook.Name = Tokens.Parse(entry.Value.GuideBook.Name);
                        entry.Value.GuideBook.Time = Tokens.Parse(entry.Value.GuideBook.Time);
                    }
                    ret[newKey] = entry.Value;
                }
                return ret;
            }, AssetLoadPriority.Exclusive);
        }
    }

    internal static string CategoryName(RecipeCategory cat)
    {
        return Translation.Get($"guidebook.category.{cat.ToString()}");
    }
}

internal class Recipe
{
    public string Id = null!;
    public string Condition = null;
    public GuideBookStrings GuideBook = null;
    public List<string> InputItems = new();
    public List<ItemSpec> OutputItems = new();
    public OutputBehavior OutputChooseBehavior = OutputBehavior.Random;
    public List<int> MatchInputIgnoreIndexes = new();
    public int MinutesToProcess = 0;
    public int Precedence = 0;
    // this -3b9mavm,. [*pab.\j == SEGMENTATION FAULT (CORE DUMPED) ==
    public string Script = null;
}

internal enum OutputBehavior
{
    Random,
    TryMatchInput,
}

internal class ItemSpec
{
    public List<string> Id = new();
    public int Quantity = 1;

    public int MatchInputItemIndex = -1;
    public bool IncreaseQuality = false;

    public string ItemQuery = null;
    public string PerItemCondition = null;
    public int MaxItems = 1;
}

internal class GuideBookStrings
{
    public RecipeCategory Category = RecipeCategory.Fusion;
    public string Name = null;
    public List<string> Inputs = new();
    public List<string> Outputs = new();
    public string Time = null;
}

internal enum RecipeCategory
{
    Fusion,
    Enhance,
    Cycling,
    MeltingPot,
}

