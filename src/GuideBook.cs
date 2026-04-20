using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.GameData.Objects;
using StardewValley.Menus;
using StardewValley.TokenizableStrings;
using System;
using System.Collections.Generic;
using System.Linq;
using ichortower.TowerCore;

using SObject = StardewValley.Object;
using TR = ichortower.TowerCore.Translation;

namespace ichortower.MetamorphicCube;

internal class GuideBook
{
    internal static string ItemId = $"{Main.ModId}_RecipeBook";
    internal static string QualifiedItemId = $"(O){GuideBook.ItemId}";
    internal static string TextureAsset = $"{Main.ModId}/Textures/GuideBook";
    internal static string StringsAsset = $"{Main.ModId}/Strings/GuideBook";


    [TargetMethod(typeof(SObject), nameof(SObject.performUseAction))]
    [PatchType(PatchTypes.Postfix)]
    internal static void Object_performUseAction_Postfix(SObject __instance,
            GameLocation location, ref bool __result)
    {
        if (!__instance.QualifiedItemId.Equals(GuideBook.QualifiedItemId)) {
            return;
        }
        if (Game1.activeClickableMenu is not null) {
            return;
        }
        GuideBookMenu gbm = new();
        Game1.activeClickableMenu = gbm;
        __instance.Stack += 1;
        __result = true;
    }

    [SmapiEvent]
    internal static void AssetRequested(object sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo("Data/Objects")) {
            e.Edit(asset => {
                var dict = asset.AsDictionary<string, ObjectData>();
                ObjectData machine = new() {
                    Name = GuideBook.ItemId,
                    DisplayName = $"[LocalizedText Strings\\Objects:{ItemId}_Name]",
                    Description = $"[LocalizedText Strings\\Objects:{ItemId}_Description]",
                    Type = "asdf",
                    Category = 0,
                    Price = 1000,
                    Edibility = -300,
                    Texture = GuideBook.TextureAsset,
                    SpriteIndex = 0,
                    CanBeTrashed = false,
                    CanBeGivenAsGift = false,
                    ContextTags = new List<string>() {"prevent_loss_on_death"},
                };
                dict.Data[machine.Name] = machine;
            }, AssetEditPriority.Default);
        }
        else if (e.Name.IsEquivalentTo("Strings/Objects")) {
            e.Edit(asset => {
                var dict = asset.AsDictionary<string, string>();
                dict.Data[$"{ItemId}_Name"] = TR.Get("recipebook.name");
                dict.Data[$"{ItemId}_Description"] = TR.Get("recipebook.description");
            });
        }
        else if (e.Name.IsEquivalentTo(GuideBook.TextureAsset)) {
            e.LoadFrom(() => {
                return Main.Helper.ModContent.Load<Texture2D>("assets/GuideBook.png");
            }, AssetLoadPriority.Exclusive);
        }
        else if (e.Name.IsEquivalentTo(GuideBook.StringsAsset)) {
            e.LoadFrom(() => {
                string lang = LocalizedContentManager.LanguageCodeString(LocalizedContentManager.CurrentLanguageCode);
                if (!String.IsNullOrEmpty(lang)) {
                    lang = "." + lang;
                }
                Dictionary<string, string> fromFile = Main.Helper.ModContent.Load
                        <Dictionary<string, string>>($"data/guidebook-strings{lang}.json");
                Dictionary<string, string> ret = new();
                foreach (var entry in fromFile) {
                    ret[entry.Key] = Tokens.Parse(entry.Value);
                }
                return ret;
            }, AssetLoadPriority.Exclusive);
        }
    }

}

internal class GuideBookMenu : LetterViewerMenu
{
    public GuideBookMenu(Dictionary<string, Recipe> data = null)
        : base("")
    {
        data ??= RecipeData.Data;

        this.mailMessage = data.Values.Where(recipe => {
            return GameStateQuery.CheckConditions(recipe.Condition);
        })
        .Select(recipe => GetParsedGuidebookOutput(recipe.GuideBook))
        .Where(s => s is not null)
        .ToList();

        this.forwardButton.visible = this.page < this.mailMessage.Count - 1;
        this.backButton.visible = this.page > 0;
        this.OnPageChange();
    }

    internal string GetParsedGuidebookOutput(GuideBookStrings g)
    {
        if (g is null) {
            return null;
        }
        string title = RecipeData.CategoryName(g.Category) + ": " +
                TokenParser.ParseText(g.Name);
        string ret = CenterfulPadding(title) + title + "^";
        string time = TokenParser.ParseText(g.Time);
        ret += CenterfulPadding(time) + time + "^";
        foreach (string s in g.Inputs) {
            ret += $"^  {TokenParser.ParseText(s)}";
        }
        ret += "^";
        foreach (string s in g.Outputs) {
            ret += $"^    > {TokenParser.ParseText(s)}";
        }
        return ret;
    }

    internal static int spaceWidth = -1;

    internal string CenterfulPadding(string s)
    {
        if (spaceWidth < 0) {
            spaceWidth = SpriteText.getWidthOfString(" ");
        }
        int stringWidth = SpriteText.getWidthOfString(s);
        int numspaces = (this.width / 2 - stringWidth / 2) / spaceWidth;
        return new string(' ', numspaces);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        int diff = -1 * Math.Sign(direction);
        int prev = this.page;
        this.page = Math.Max(0, Math.Min(this.page + diff, this.mailMessage.Count - 1));
        if (prev != this.page) {
            Game1.playSound("shwip");
            this.OnPageChange();
        }
    }
}
