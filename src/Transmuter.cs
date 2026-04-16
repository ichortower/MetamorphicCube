using ichortower.TowerCore;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Extensions;
using StardewValley.GameData.BigCraftables;
using StardewValley.Internal;
using StardewValley.Inventories;
using StardewValley.Menus;
using StardewValley.Network;
using StardewValley.Objects;
using System;
using System.Collections.Generic;
using System.Linq;

using SObject = StardewValley.Object;
using TR = ichortower.TowerCore.Translation;

namespace ichortower.MetamorphicCube;

internal class Transmuter
{
    internal static string ItemId = $"{Main.ModId}_MetamorphicCube";
    internal static string QualifiedItemId = $"(BC){Transmuter.ItemId}";
    internal static string MainStorageKey = $"{Main.ModId}_TransmuterMainStorage";
    internal static string BufferStorageKey = $"{Main.ModId}_TransmuterBufferStorage";
    internal static string TextureAsset = $"{Main.ModId}/Textures/MetamorphicCube";
    internal static string SpecialStatusKey = $"{Main.ModId}/Override";

    internal const int Capacity = 9;

    internal const string MenuOpenSound = "bigSelect";
    internal const string TransmutingStartSound = "yoba";
    internal const string TransmutingFinishSound = "dwop";
    // candidates: crit, yoba, jingle1, junimoKart_coin, metal_tap
    internal const string DepositItemSound = "crit";
    internal const string WithdrawItemSound = "coin";
    internal const string ErrorSound = "cancel";


    [TargetMethod(typeof(SObject), nameof(SObject.checkForAction))]
    [PatchType(PatchTypes.Postfix)]
    internal static void Object_checkForAction_Postfix(SObject __instance,
            ref bool __result, Farmer who, bool justCheckingForActivity)
    {
        if (__instance.QualifiedItemId != Transmuter.QualifiedItemId) {
            return;
        }
        if (justCheckingForActivity) {
            __result = true;
            return;
        }
        if (who.ActiveObject is not null) {
            __result = false;
            return;
        }
        if (__instance.MinutesUntilReady > 0) {
            Game1.playSound(Transmuter.ErrorSound);
            __instance.shakeTimer = 100;
            return;
        }
        if (__instance.modData.ContainsKey(Transmuter.SpecialStatusKey)) {
            // FIXME multiplayer god no
            Embedded.Warp(__instance, AddCleanupListener);
            return;
        }
        if (Game1.activeClickableMenu is not null) {
            return;
        }
        GetInventoryMutex(__instance).RequestLock(() => {
            Game1.activeClickableMenu = new TransmuterMenu(__instance);
            Game1.activeClickableMenu.exitFunction = () => {
                GetInventoryMutex(__instance).ReleaseLock();
            };
            Game1.playSound(Transmuter.MenuOpenSound);
        }, () => {
            Game1.playSound(Transmuter.ErrorSound);
            __instance.shakeTimer = 100;
        });
    }

    [TargetMethod(typeof(SObject), nameof(SObject.draw),
                  new Type[] {typeof(SpriteBatch), typeof(int), typeof(int), typeof(float)})]
    [PatchType(PatchTypes.Prefix)]
    internal static void Object_draw_Prefix(SObject __instance, ref bool __state)
    {
        __state = __instance.hovering;
    }

    [TargetMethod(typeof(SObject), nameof(SObject.draw),
                  new Type[] {typeof(SpriteBatch), typeof(int), typeof(int), typeof(float)})]
    [PatchType(PatchTypes.Postfix)]
    internal static void Object_draw_Postfix(SObject __instance, ref bool __state,
            SpriteBatch spriteBatch, int x, int y, float alpha)
    {
        if (__instance.QualifiedItemId != Transmuter.QualifiedItemId) {
            return;
        }
        if (!__instance.modData.TryGetValue(Transmuter.SpecialStatusKey, out string _)) {
            return;
        }
        if (!__state) {
            return;
        }
        Vector2 position = Game1.GlobalToLocal(Game1.viewport,
                new Vector2(x * 64 + 32, y * 64 - 64));
        SpriteText.drawSmallTextBubble(spriteBatch, "?????", position, 256,
                0.98f + __instance.TileLocation.X * 0.0001f + __instance.TileLocation.Y * 1e-6f);
    }


    /*
     * This patch allows working time to pass for the transmuter (it's not a conventional
     * machine and doesn't hit the code that counts down minutesUntilReady).
     */
    [TargetMethod(typeof(SObject), nameof(SObject.minutesElapsed))]
    [PatchType(PatchTypes.Postfix)]
    internal static void Object_minutesElapsed_Postfix(SObject __instance,
            bool __result, int minutes)
    {
        if (__instance.QualifiedItemId != Transmuter.QualifiedItemId) {
            return;
        }
        if (__instance.MinutesUntilReady > 0) {
            __instance.MinutesUntilReady -= minutes;
            if (__instance.MinutesUntilReady <= 0) {
                SwapToOutputInventory(__instance);
                MachineSound(__instance, Transmuter.TransmutingFinishSound);
            }
        }
    }

    internal static void MachineSound(SObject instance, string cue, int pitch = 1200)
    {
        instance?.Location?.playSound(cue, instance?.TileLocation, pitch);
    }

    internal static void SwapToOutputInventory(SObject instance)
    {
        Inventory source = GetInventory(instance, Transmuter.MainStorageKey);
        Inventory buffer = GetInventory(instance, Transmuter.BufferStorageKey);
        source.ReplaceWithClonesFrom(buffer);
        buffer.Clear();
    }

    internal static bool TryStartTransmutation(SObject instance, out bool shouldCloseMenu)
    {
        shouldCloseMenu = false;
        Inventory source = GetInventory(instance, Transmuter.MainStorageKey);
        Inventory buffer = GetInventory(instance, Transmuter.BufferStorageKey);
        Inventory temp = new();
        // see Extensions.cs
        temp.ReplaceWithClonesFrom(source);
        int crunchTime = 0;

        var dataAsList = RecipeData.Data.ToList();
        dataAsList.Sort((a, b) => b.Value.Precedence.CompareTo(a.Value.Precedence));
        Recipe shortCircuit = Embedded.TheRecipe;
        dataAsList.Insert(0, new(shortCircuit.Id, shortCircuit));
        bool anythingProcessed = false;
        foreach (var pair in dataAsList) {
            while (TryAcceptRecipe(pair.Value, temp, buffer, ref crunchTime)) {
                anythingProcessed = true;
                if (!System.Object.ReferenceEquals(shortCircuit, pair.Value)) {
                    continue;
                }
                if (pair.Value.Script is not null) {
                    Script s = new(instance, pair.Value.Script);
                    s.OnComplete = delegate {
                        source.Clear();
                        buffer.Clear();
                        if (instance is not null) {
                            instance.MinutesUntilReady = 0;
                            instance.modData[Transmuter.SpecialStatusKey] = "yes";
                        }
                    };
                    Script.Run(s);
                    // this will have plenty of time to finish
                    Embedded.LoadAudio();
                }
                goto checksDone;
            }
        }
        checksDone:
        if (!anythingProcessed) {
            return false;
        }
        foreach (Item item in temp) {
            buffer.AddWithStacking(item);
        }
        if (crunchTime > 0) {
            instance.MinutesUntilReady = crunchTime;
            shouldCloseMenu = true;
        }
        else {
            SwapToOutputInventory(instance);
        }
        MachineSound(instance, Transmuter.TransmutingStartSound);
        return true;
    }

    internal static bool TryAcceptRecipe(Recipe recipe, Inventory source,
            Inventory dest, ref int minutesToProcess)
    {
        if (!GameStateQuery.CheckConditions(recipe.Condition)) {
            return false;
        }
        if (recipe.InputItems.Count == 0) {
            Log.Warn($"Recipe '{recipe.Id}' has no inputs.");
            return false;
        }
        if (recipe.Script is null && recipe.OutputItems.Count == 0) {
            Log.Warn($"Recipe '{recipe.Id}' has no outputs.");
            return false;
        }
        // the source inventory is copied into a temp bucket so we can non-destructively
        // remove items from it as they are pulled for recipes
        Inventory tempSource = new();
        tempSource.ReplaceWithClonesFrom(source);
        List<int> ignorePullsFrom = recipe.MatchInputIgnoreIndexes ?? new();
        List<int> pulledIndexes = new();
        List<Item> lateReferences = new();
        // "default" is iridium quality because it gets Min'ed with source qualities
        int outputQuality = 4;
        for (int q = 0; q < recipe.InputItems.Count; ++q) {
            string query = recipe.InputItems[q];
            // get required stack size and potential item ids, if relevant
            int minStack = 1;
            string[] matchIds = new string[0];
            string[] rawParts = GameStateQuery.SplitRaw(query);
            foreach (string subq in rawParts) {
                string[] args = ArgUtility.SplitBySpaceQuoteAware(subq);
                if (args[0].EqualsIgnoreCase("ITEM_STACK")) {
                    if (!ArgUtility.TryGetInt(args, 2, out minStack, out string err)) {
                        Log.Warn(err);
                    }
                }
                else if (args[0].EqualsIgnoreCase("ITEM_ID")) {
                    if (args.Length >= 3) {
                        matchIds = args[2..];
                    }
                }
            }
            // actually check the recipe and deduct matched items
            bool found = false;
            for (int i = 0; i < tempSource.Count; ++i) {
                if (!GameStateQuery.CheckConditions(query, targetItem: tempSource[i])) {
                    continue;
                }
                found = true;
                // whether to ignore this item for quality and index matching 
                int pIndex = Array.IndexOf(matchIds, tempSource[i].QualifiedItemId);
                if (!ignorePullsFrom.Contains(q)) {
                    pulledIndexes.Add(pIndex);
                    outputQuality = Math.Min(outputQuality, tempSource[i].Quality);
                }
                lateReferences.Add(tempSource[i]);
                if (tempSource[i].ConsumeStack(minStack) is null) {
                    tempSource[i] = null;
                }
                break;
            }
            if (!found) {
                return false;
            }
        }
        // back in the loop we left nulls lying around. they will cause barfing later, so
        // let's remove them
        tempSource.RemoveEmptySlots();
        // if we made it here, then every source item was found and successfully
        // removed from the temp inventory

        // first, generate outputs in a transient inventory
        Inventory outputs = new();
        for (int i = 0; i < recipe.OutputItems.Count; ++i) {
            // use item query if requested
            string iq = recipe.OutputItems[i].ItemQuery;
            if (!String.IsNullOrEmpty(iq)) {
                int maxItems = recipe.OutputItems[i].MaxItems;
                string perItemCondition = recipe.OutputItems[i].PerItemCondition;
                ItemQueryResult[] got = ItemQueryResolver.TryResolve(iq, context: null,
                        perItemCondition: perItemCondition,
                        maxItems: maxItems,
                        avoidRepeat: true);
                foreach (ItemQueryResult res in got) {
                    outputs.Add((Item)res.Item);
                }
                continue;
            }
            Item n;
            // check for the use-input-item-and-increase-quality special output
            int idx = recipe.OutputItems[i].MatchInputItemIndex;
            if (idx >= 0 && recipe.OutputItems[i].IncreaseQuality) {
                n = lateReferences[idx].getOne();
                n.Quality = n.Quality switch {
                    0 => 1,
                    1 => 2,
                    2 => 4,
                    _ => 4,
                };
                n.Stack = recipe.OutputItems[i].Quantity;
            }
            // standard output here
            else {
                int useIndex = Game1.random.Next(recipe.OutputItems[i].Id.Count);
                if (recipe.OutputChooseBehavior == OutputBehavior.TryMatchInput) {
                    int[] choices = pulledIndexes.Where(
                            idx => idx < recipe.OutputItems[i].Id.Count).ToArray();
                    useIndex = Game1.random.ChooseFrom(choices);
                }
                n = ItemRegistry.Create(recipe.OutputItems[i].Id[useIndex],
                        amount: recipe.OutputItems[i].Quantity,
                        quality: outputQuality);
            }
            outputs.Add(n);
        }

        // check for sufficient space in final output by fitting in another temp inventory
        Inventory tempDest = new();
        tempDest.ReplaceWithClonesFrom(dest);
        foreach (Item item in tempSource) {
            tempDest.AddWithStacking(item.Clone());
        }
        foreach (Item item in outputs) {
            tempDest.AddWithStacking(item.Clone());
        }
        if (tempDest.Count > Transmuter.Capacity) {
            return false;
        }

        // finally, put outputs in dest, update source, set process time
        foreach (Item item in outputs) {
            dest.AddWithStacking(item);
        }
        source.ReplaceWithClonesFrom(tempSource);
        minutesToProcess = Math.Max(minutesToProcess, recipe.MinutesToProcess);
        return true;
    }

    // only pass the static storage key strings to this.
    // maybe i should make it an enum? eh
    internal static string GetStorageId(SObject instance, string key)
    {
        if (instance.modData.TryGetValue(key, out string existing)) {
            return existing;
        }
        string ret = Guid.NewGuid().ToString();
        instance.modData[key] = ret;
        return ret;
    }

    internal static Inventory GetInventory(SObject instance, string key)
    {
        string storageId = GetStorageId(instance, key);
        return Game1.player.team.GetOrCreateGlobalInventory(storageId);
    }

    internal static NetMutex GetInventoryMutex(SObject instance)
    {
        string storageId = GetStorageId(instance, Transmuter.MainStorageKey);
        return Game1.player.team.GetOrCreateGlobalInventoryMutex(storageId);
    }

    internal static System.EventHandler<OneSecondUpdateTickedEventArgs> Cleaner = null;

    internal static void AddCleanupListener()
    {
        if (Cleaner is not null) {
            return;
        }
        Cleaner = Transmuter.CleanupListener;
        Main.Helper.Events.GameLoop.OneSecondUpdateTicked += Cleaner;
    }

    internal static void CleanupListener(object sender, OneSecondUpdateTickedEventArgs e)
    {
        if (Embedded.TheLocation.farmers.Any()) {
            return;
        }
        Main.Helper.Events.GameLoop.OneSecondUpdateTicked -= Cleaner;
        Cleaner = null;
        Embedded.UnloadTheLocation();

        Utility.ForEachLocation((GameLocation loc) => {
            bool found = false;
            var objs = loc.Objects.Values.Where(obj =>
                    obj.QualifiedItemId.Equals(Transmuter.QualifiedItemId));
            foreach (SObject obj in objs) {
                obj.modData.Remove(Transmuter.SpecialStatusKey);
                found = true;
            }
            if (found) {
                Script.RemoveScriptTas(loc);
            }
            return true;
        });
    }


    [SmapiEvent]
    internal static void AssetRequested(object sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo("Data/BigCraftables")) {
            e.Edit(asset => {
                var dict = asset.AsDictionary<string, BigCraftableData>();
                BigCraftableData machine = new() {
                    Name = Transmuter.ItemId,
                    DisplayName = $"[LocalizedText Strings\\BigCraftables:{ItemId}_Name]",
                    Description = $"[LocalizedText Strings\\BigCraftables:{ItemId}_Description]",
                    Price = 1000,
                    Fragility = 0,
                    IsLamp = false,
                    Texture = Transmuter.TextureAsset,
                    SpriteIndex = 0,
                };
                dict.Data[machine.Name] = machine;
            }, AssetEditPriority.Default);
        }
        else if (e.Name.IsEquivalentTo("Strings/BigCraftables")) {
            e.Edit(asset => {
                var dict = asset.AsDictionary<string, string>();
                dict.Data[$"{ItemId}_Name"] = TR.Get("cube.name");
                dict.Data[$"{ItemId}_Description"] = TR.Get("cube.description");
            }, AssetEditPriority.Default);
        }
        else if (e.Name.IsEquivalentTo(Transmuter.TextureAsset)) {
            e.LoadFrom(() => {
                return Main.Helper.ModContent.Load<Texture2D>("assets/MetamorphicCube.png");
            }, AssetLoadPriority.Exclusive);
        }
        else if (e.Name.IsEquivalentTo("Data/CraftingRecipes")) {
            e.Edit(asset => {
                var dict = asset.AsDictionary<string, string>();
                string key = Transmuter.ItemId;
                string recipe = $"337 3 709 15 872 3/Home/{Transmuter.ItemId}/true/null/";
                dict.Data[key] = recipe;
            }, AssetEditPriority.Default);
        }
    }

    [SmapiEvent]
    internal static void ObjectListChanged(object sender, ObjectListChangedEventArgs e)
    {
        foreach (var kvp in e.Removed) {
            if (kvp.Value.QualifiedItemId != Transmuter.QualifiedItemId) {
                continue;
            }
            string inventoryId = GetStorageId(kvp.Value, Transmuter.MainStorageKey);
            foreach (Item item in Game1.player.team.GetOrCreateGlobalInventory(inventoryId)) {
                e.Location.debris.Add(new Debris(item, kvp.Key * 64f + new Vector2(32f, 32f)));
            }
            Script.RemoveScriptTas();
            Script.Stop();
            Game1.player.team.globalInventories.Remove(inventoryId);
            Game1.player.team.globalInventories.Remove(
                    GetStorageId(kvp.Value, Transmuter.BufferStorageKey));
        }
    }

    [SmapiEvent]
    internal static void Warped(object sender, WarpedEventArgs e)
    {
        var objs = e.NewLocation.Objects.Values.Where((obj) => {
            return obj.QualifiedItemId.Equals(Transmuter.QualifiedItemId) &&
                    obj.modData.ContainsKey(Transmuter.SpecialStatusKey);
        });
        foreach (SObject o in objs) {
            TryAddFinalSprite(o, e.NewLocation);
        }
    }

    internal static void TryAddFinalSprite(SObject instance, GameLocation loc)
    {
        Vector2 targetPos = (instance.TileLocation + new Vector2(-0.5f, -1.5f)) * 64f;
        if (loc.TemporarySprites.Any((sprite) => {
            return sprite.extraInfoForEndBehavior == Script.SpecialNumber &&
                    sprite.position.Equals(targetPos);
        })) {
            return;
        }
        Rectangle sourceRect = new(0, 128, 32, 48);
        TemporaryAnimatedSprite sprite = new(Embedded.Strings["Tas.TextureName"],
                                             sourceRect: sourceRect,
                                             animationInterval: 120,
                                             animationLength: 6,
                                             numberOfLoops: 9999,
                                             position: targetPos,
                                             flicker: false,
                                             flipped: false) {
             scale = 4f,
             layerDepth = (instance.TileLocation.Y + 1f) * 64f / 10000f,
             extraInfoForEndBehavior = Script.SpecialNumber,
        };
        Game1.Multiplayer.broadcastSprites(loc, sprite);
    }
}
