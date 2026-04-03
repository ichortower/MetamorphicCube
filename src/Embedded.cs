using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json;
using SkiaSharp;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Locations;
using StardewValley.Monsters;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Threading;
using xTile.Format;
using xTile.Tiles;

using ichortower.TowerCore;
using SObject = StardewValley.Object;

namespace ichortower.MetamorphicCube;


internal class Embedded
{
    internal static string Key {
        get {
            return "zalgo";
        }
    }

    internal static byte[] Unlock(byte[] data)
    {
        byte[] ret = new byte[data.Length];
        for (int i = 0; i < data.Length; ++i) {
            byte k = (byte)(Key[i % Key.Length] & 0xff);
            ret[i] = (byte)(k ^ data[i]);
        }
        return ret;
    }

    internal static Stream GetManifestStream(string name)
    {
        Assembly a = Assembly.GetExecutingAssembly();
        return a.GetManifestResourceStream($"{a.GetName().Name}.{name}");
    }

    internal static string GetDecodedString(string name)
    {
        byte[] unlocked = Unlock(GetManifestStream(name).ReadAllBytes());
        return System.Text.Encoding.UTF8.GetString(unlocked);
    }

    // the amount of shunting between byte[] and stream this does is getting comical
    internal static MemoryStream GetDecodedStream(string name)
    {
        byte[] unlocked = Unlock(GetManifestStream(name).ReadAllBytes());
        return new MemoryStream(unlocked);
    }

    // hey do you love reimplementing the bonkers stuff smapi does for you? i love it it's so cool
    internal static Texture2D GetDecodedTexture(string name)
    {
        MemoryStream stream = GetDecodedStream(name);
        SKBitmap bitmap = SKBitmap.Decode(stream);
        SKPMColor[] rawPixels = SKPMColor.PreMultiply(bitmap.Pixels);
        Color[] monoPixels = new Color[rawPixels.Length];
        for (int i = 0; i < monoPixels.Length; ++i) {
            SKPMColor p = rawPixels[i];
            monoPixels[i] = p.Alpha switch {
                0 => Color.Transparent,
                _ => new Color(r: p.Red, g: p.Green, b: p.Blue, alpha: p.Alpha),
            };
        }
        Texture2D ret = new(Game1.graphics.GraphicsDevice, bitmap.Width, bitmap.Height);
        ret.SetData(monoPixels);
        return ret;
    }


    private static Dictionary<string, string> _strings = null;
    internal static Dictionary<string, string> Strings {
        get {
            if (_strings is not null) {
                return _strings;
            }
            string s = GetDecodedString("embedded.data.strings");
            _strings = JsonConvert.DeserializeObject<Dictionary<string, string>>(s);
            if (_strings.TryGetValue("Mod.Id", out string modId)) {
                foreach (string key in _strings.Keys) {
                    _strings[key] = _strings[key].Replace("{Mod.Id}", modId);
                }
            }
            return _strings;
        }
    }

    private static Dictionary<string, string> _monsters = null;
    internal static Dictionary<string, string> Monsters {
        get {
            if (_monsters is not null) {
                return _monsters;
            }
            string s = GetDecodedString("embedded.data.monsters");
            var temp = JsonConvert.DeserializeObject<Dictionary<string, string>>(s);
            if (!Strings.TryGetValue("Mod.Id", out string modId)) {
                _monsters = temp;
                return _monsters;
            }
            _monsters = new();
            foreach (string key in temp.Keys) {
                string newkey = key.Replace("{Mod.Id}", modId);
                _monsters[newkey] = temp[key].Replace("{Mod.Id}", modId);
            }
            return _monsters;
        }
    }

    private static Texture2D _bruteTexture = null;
    internal static Texture2D BruteTexture {
        get {
            _bruteTexture ??= GetDecodedTexture("embedded.gfx.brute");
            return _bruteTexture;
        }
    }

    private static Texture2D _sorcererTexture = null;
    internal static Texture2D SorcererTexture {
        get {
            _sorcererTexture ??= GetDecodedTexture("embedded.gfx.sorcerer");
            return _sorcererTexture;
        }
    }

    private static Texture2D _bossTexture = null;
    internal static Texture2D BossTexture {
        get {
            _bossTexture ??= GetDecodedTexture("embedded.gfx.boss");
            return _bossTexture;
        }
    }


    private static Recipe _theRecipe = null;
    internal static Recipe TheRecipe {
        get {
            string s = GetDecodedString("embedded.data.recipe");
            _theRecipe = JsonConvert.DeserializeObject<Recipe>(s);
            if (Strings.TryGetValue("Mod.Id", out string modId)) {
                _theRecipe.Id = _theRecipe.Id.Replace("{Mod.Id}", modId);
            }
            return _theRecipe;
        }
    }

    private static Texture2D _tasTexture = null;
    internal static Texture2D Animations {
        get {
            _tasTexture ??= Texture2D.FromStream(Game1.graphics.GraphicsDevice,
                    GetDecodedStream("embedded.gfx.tas"));
            return _tasTexture;
        }
    }

    private static xTile.Map _theMap = null;
    internal static xTile.Map TheMap {
        get {
            if (_theMap is not null) {
                return _theMap;
            }
            _theMap ??= FormatManager.Instance.BinaryFormat.Load(
                    GetDecodedStream("embedded.data.map"));
            // extremely naive version of what smapi does to fix tilesheet paths,
            // accounting only for our use case
            foreach (TileSheet tilesheet in _theMap.TileSheets) {
                tilesheet.ImageSource = Path.Combine("Maps", tilesheet.ImageSource);
                if (tilesheet.ImageSource.EndsWith(".png")) {
                    tilesheet.ImageSource = tilesheet.ImageSource[..^4];
                }
            }
            return _theMap;
        }
    }

    internal static bool _registeredActions = false;
    internal static GameLocation _theLocation = null;
    internal static GameLocation TheLocation {
        get {
            if (_theLocation is null) {
                _theLocation = new GameLocation(Embedded.Strings["Map.AssetName"],
                        Embedded.Strings["Location.Id"]);
                _theLocation.DisplayName = Embedded.Strings["Location.Name"];
                // force reload the map to undo our tile shenanigans
                _theLocation.loadMap(_theLocation.mapPath.Value, force_reload: true);
                if (!_registeredActions) {
                    GameLocation.RegisterTouchAction(Embedded.Strings["Mod.Id"] + "_SpringTrap",
                            Embedded.SpringTrap);
                    GameLocation.RegisterTileAction(Embedded.Strings["Mod.Id"] + "_WarpHome",
                            Embedded.WarpHome);
                    _registeredActions = true;
                }
                // use skull cavern time speed
                _theLocation.ExtraMillisecondsPerInGameMinute = 200;
                SpawnFromMap();
            }
            return _theLocation;
        }
    }

    internal static void UnloadTheLocation()
    {
        _theMap = null;
        if (_theLocation is null) {
            return;
        }
        _theLocation.characters.Clear();
        _theLocation.Objects.Clear();
        _theLocation.TemporarySprites.Clear();
        _theLocation.Animals.Clear();

        _theLocation = null;
    }

    internal static bool _audioLoaded = false;
    // unlike most stuff here, this is not on-demand but must be called in advance
    // (it will do its work on a background thread)
    // don't forget to call it!
    internal static void LoadAudio()
    {
        if (_audioLoaded) {
            return;
        }
        Thread t = new(() => {
            CueDefinition BruteSound = Ogg.CueFromStreams(new Stream[] {
                GetDecodedStream("embedded.audio.m1"),
                GetDecodedStream("embedded.audio.m2"),
                GetDecodedStream("embedded.audio.m3"),
                GetDecodedStream("embedded.audio.m4"),
                GetDecodedStream("embedded.audio.m5"),
            }, Embedded.Strings["Monster.Brute.ChargeSound"], category:"Sound", looped: false);
            Game1.soundBank.AddCue(BruteSound);
            CueDefinition BossMove1 = Ogg.CueFromStream(GetDecodedStream("embedded.audio.b1"),
                    Embedded.Strings["Monster.Boss.HadoukenSound"], category: "Sound", looped: false);
            Game1.soundBank.AddCue(BossMove1);
            CueDefinition BossMove2 = Ogg.CueFromStream(GetDecodedStream("embedded.audio.b2"),
                    Embedded.Strings["Monster.Boss.ShoryukenSound"], category: "Sound", looped: false);
            Game1.soundBank.AddCue(BossMove2);
            CueDefinition BossDie = Ogg.CueFromStream(GetDecodedStream("embedded.audio.b3"),
                    Embedded.Strings["Monster.Boss.DieSound"], category: "Sound", looped: false);
            Game1.soundBank.AddCue(BossDie);
            _audioLoaded = true;
        });
        t.Start();
    }

    internal static void AssetRequested(object sender, AssetRequestedEventArgs e)
    {
        if (e.Name.IsEquivalentTo(Embedded.Strings["Map.AssetName"])) {
            WithFakeName(() => {
                e.LoadFrom(() => Embedded.TheMap, AssetLoadPriority.Exclusive);
            }, e.Name);
        }
        if (e.Name.IsEquivalentTo(Embedded.Strings["Tas.TextureName"])) {
            WithFakeName(() => {
                e.LoadFrom(() => Embedded.Animations, AssetLoadPriority.Exclusive);
            }, e.Name);
        }
        if (e.Name.IsEquivalentTo("Data/Monsters")) {
            WithFakeName(() => {
                e.Edit((asset) => {
                    var dict = asset.AsDictionary<string, string>();
                    foreach (var entry in Embedded.Monsters) {
                        dict.Data[entry.Key] = entry.Value;
                    }
                });
            }, e.Name);
        }
        if (e.Name.IsEquivalentTo($"Characters/Monsters/{Embedded.Strings["Monster.Brute.Id"]}")) {
            WithFakeName(() => {
                e.LoadFrom(() => Embedded.BruteTexture, AssetLoadPriority.Exclusive);
            }, e.Name);
        }
        if (e.Name.IsEquivalentTo($"Characters/Monsters/{Embedded.Strings["Monster.Sorcerer.Id"]}")) {
            WithFakeName(() => {
                e.LoadFrom(() => Embedded.SorcererTexture, AssetLoadPriority.Exclusive);
            }, e.Name);
        }
        if (e.Name.IsEquivalentTo($"Characters/Monsters/{Embedded.Strings["Monster.Boss.Id"]}")) {
            WithFakeName(() => {
                e.LoadFrom(() => Embedded.BossTexture, AssetLoadPriority.Exclusive);
            }, e.Name);
        }
    }

    internal static void WithFakeName(Action func, IAssetName asset)
    {
        Spoof.Target = asset.ToString();
        Spoof.SetModName(Embedded.Strings["Mod.Name"]);
        func();
    }

    internal static void ReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
    {
        UnloadTheLocation();
    }

    internal static void DayEnding(object sender, DayEndingEventArgs e)
    {
        UnloadTheLocation();
    }

    #if DEBUG
    internal static void CheatCode(string command, string[] args)
    {
        Embedded.LoadAudio();
        GameLocation loc = Embedded.TheLocation;
        loc.characters.Clear();
        LocationRequest req = new(Embedded.Strings["Location.Id"], isStructure: false, Embedded.TheLocation);
        req.OnWarp += () => {
            string key = Embedded.Strings["Location.Gate.Key"];
            loc.modData[key] = "3";
            loc.TryOpenInteriorGate();
            Transmuter.AddCleanupListener();
        };
        Game1.warpFarmer(req, 41, 33, 0);
    }
    #endif

    internal static bool _warping = false;
    internal static void Warp(SObject instance, Script.CompleteCallback onWarp)
    {
        if (_warping) {
            return;
        }
        _warping = true;
        TemporaryAnimatedSpriteList list = new();
        for (int i = 0; i < 12; ++i) {
            double rad = Game1.random.NextDouble() * 2 * Math.PI;
            list.Add(new TemporaryAnimatedSprite(6, instance.TileLocation * 64f, new Color(125, 0, 0)) {
                motion = new Vector2((float)Math.Cos(rad) * 3.8f, (float)Math.Sin(rad) * 2.8f),
                ticksBeforeAnimationStart = i*2,
            });
        }
        Game1.freezeControls = true;
        Game1.displayFarmer = false;
        Game1.player.CanMove = false;
        Game1.Multiplayer.broadcastSprites(instance.Location, list);
        instance.Location.playSound("wand", instance.TileLocation);
        DelayedAction.fadeAfterDelay(() => {
            Game1.viewport.X = -1000 * 64;
            Game1.viewport.Y = -1000 * 64;
            Game1.viewportFreeze = true;
            LocationRequest req = new(Embedded.Strings["Location.Id"], isStructure: false, Embedded.TheLocation);
            req.OnWarp += delegate {
                _warping = false;
                Game1.viewportFreeze = false;
                Game1.freezeControls = false;
                Game1.displayFarmer = true;
                Game1.player.CanMove = true;
                onWarp.Invoke();
                DelayedAction.functionAfterDelay(Embedded.TitleCard, 1000);
            };
            Game1.warpFarmer(req, 14, 76, 0);
        }, 1000);
    }

    internal static void TitleCard()
    {
        Rectangle sourceRect = new(0, 176, 200, 20);
        Vector2 pos = new(Game1.viewport.Width / 2 - sourceRect.Width * 4 / 2,
                                Game1.viewport.Height / 2 - sourceRect.Height * 4 / 2);
        pos.X += Game1.viewport.X;
        pos.Y += Game1.viewport.Y;
        TemporaryAnimatedSprite fadeIn = new(Embedded.Strings["Tas.TextureName"],
                                             sourceRect: sourceRect,
                                             animationInterval: 1500,
                                             animationLength: 1,
                                             numberOfLoops: 1,
                                             position: pos,
                                             flicker: false,
                                             flipped: false) {
             alpha = 0f,
             alphaFade = -0.03f,
             scale = 4f,
        };
        TemporaryAnimatedSprite hold = new(Embedded.Strings["Tas.TextureName"],
                                             sourceRect: sourceRect,
                                             animationInterval: 3200,
                                             animationLength: 1,
                                             numberOfLoops: 1,
                                             position: pos,
                                             flicker: false,
                                             flipped: false) {
             delayBeforeAnimationStart = 1500,
             scale = 4f,
        };
        TemporaryAnimatedSprite fadeOut = new(Embedded.Strings["Tas.TextureName"],
                                             sourceRect: sourceRect,
                                             animationInterval: 1500,
                                             animationLength: 1,
                                             numberOfLoops: 1,
                                             position: pos,
                                             flicker: false,
                                             flipped: false) {
             alphaFade = 0.03f,
             delayBeforeAnimationStart = 1500 + 3200,
             scale = 4f,
        };
        Game1.currentLocation.temporarySprites.Add(fadeIn);
        Game1.currentLocation.temporarySprites.Add(hold);
        Game1.currentLocation.temporarySprites.Add(fadeOut);
    }

    internal static void SpawnFromMap()
    {
        GameLocation loc = TheLocation;
        xTile.Layers.Layer back = loc.map.RequireLayer("Back");
        for (int x = 0; x < back.LayerWidth; ++x) {
            for (int y = 0; y < back.LayerHeight; ++y) {
                if (back.Tiles[x, y] is not xTile.Tiles.Tile tile) {
                    continue;
                }
                string prop;
                if (tile.Properties.TryGetValue("SpawnKey", out prop)) {
                    Spawn(loc, x, y, prop, makeOneSpecial: true);
                }
                if (tile.Properties.TryGetValue("Spawn", out prop)) {
                    Spawn(loc, x, y, prop);
                }
            }
        }
    }

    internal static void Spawn(GameLocation loc, int x, int y, string prop, bool makeOneSpecial = false)
    {
        string[] args = ArgUtility.SplitBySpace(prop);
        float radius = 0f;
        int numBrutes = 0;
        int numSorcerers = 0;
        for (int i = 0; i < args.Length; i += 2) {
            if (!ArgUtility.TryGetInt(args, i+1, out int val, out string error)) {
                Log.Error(error);
                return;
            }
            switch (args[i]) {
            case "Radius":
                radius = (float)Math.Abs(val);
                break;
            case "Brute":
                numBrutes = val;
                break;
            case "Sorcerer":
                numSorcerers = val;
                break;
            default:
                Log.Error($"Spawn: Unknown parameter '{args[i]}'");
                break;
            }
        }

        int chosen = makeOneSpecial switch {
            true => Game1.random.Next(numBrutes + numSorcerers),
            _ => -1,
        };
        int c = 0;
        for (int i = 0; i < numBrutes; ++i, ++c) {
            float angle = (float)(Game1.random.NextDouble() * 2f * Math.PI);
            float dist = (float)(Game1.random.NextDouble() * radius);
            Vector2 pos = new Vector2((float)x + MathF.Cos(angle) * dist,
                                      (float)y + MathF.Sin(angle) * dist * 0.8f) * 64f;
            Brute b = new(pos);
            loc.characters.Add(b);
            if (c == chosen) {
                b.MakeSpecial();
            }
        }
        for (int i = 0; i < numSorcerers; ++i, ++c) {
            float angle = (float)Game1.random.NextDouble() * 2f * MathF.PI;
            float dist = (float)(Game1.random.NextDouble() * radius);
            Vector2 pos = new Vector2((float)x + MathF.Cos(angle) * dist,
                                      (float)y + MathF.Sin(angle) * dist * 0.8f) * 64f;
            Sorcerer s = new(pos);
            loc.characters.Add(s);
            if (c == chosen) {
                s.MakeSpecial();
            }
        }
    }

    internal static bool WarpHome(GameLocation loc, string[] args, Farmer who, Point tile)
    {
        Point porch = Utility.getHomeOfFarmer(who).getFrontDoorSpot();
        loc.performTouchAction($"MagicWarp Farm {porch.X} {porch.Y}", who.Position);
        return true;
    }

    // get-fucked-its-hardcoded.jpg
    internal static void SpringTrap(GameLocation loc, string[] args, Farmer who, Vector2 position)
    {
        Rectangle zone = new(35, 14, 14, 13);
        if (loc.farmers.Any(f => !zone.Contains(f.TilePoint))) {
            return;
        }
        loc.removeTileProperty(41, 26, "Back", "TouchAction");
        loc.removeTileProperty(42, 26, "Back", "TouchAction");
        loc.setMapTile(41, 27, 848, "Buildings", "outdoors");
        loc.setMapTile(42, 27, 849, "Buildings", "outdoors");
        loc.setMapTile(41, 28, 873, "Buildings", "outdoors");
        loc.setMapTile(42, 28, 874, "Buildings", "outdoors");
        loc.playSound(Embedded.Strings["Location.Trap.Sound"]);
        Game1.changeMusicTrack(Embedded.Strings["Location.Boss.Music"]);
        loc.characters.Add(new Sorcerer(new Vector2(37, 17) * 64f));
        loc.characters.Add(new Sorcerer(new Vector2(46, 17) * 64f));
        Boss theguy = new(new Vector2(41, 16) * 64f);
        theguy.faceDirection(2);
        loc.characters.Add(theguy);
    }
}
