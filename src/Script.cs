using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Extensions;
using System;
using System.Reflection;

using SObject = StardewValley.Object;
using ichortower.TowerCore;

namespace ichortower.MetamorphicCube;

internal class Script
{
    internal delegate void CompleteCallback();

    internal const int SpecialNumber = 23675;
    internal const string ThunderSound = "thunder_small";

    internal static Script ActiveScript = null;
    internal static System.EventHandler<UpdateTickedEventArgs> Runner = null;

    internal string[] Commands;
    internal int CommandIndex;
    internal int PauseTimer;
    internal GameLocation Location;
    internal SObject Transmuter;
    internal Vector2 TilePosition;
    internal int ShakeTimer;
    internal int ShakeIntensity;
    internal CompleteCallback OnComplete;
    internal bool Thundering;
    internal int ThunderTimer;
    internal int SparkleTimer;

    public Script(SObject transmuter, string unbrokenScript)
    {
        this.Commands = ArgUtility.SplitQuoteAware(unbrokenScript, '/',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries,
                keepQuotesAndEscapes: true);
        this.CommandIndex = 0;
        this.PauseTimer = 0;
        this.ShakeTimer = 0;
        this.ShakeIntensity = 0;
        this.Transmuter = transmuter;
        this.Location = transmuter.Location;
        this.TilePosition = transmuter.TileLocation;
        this.Thundering = false;
        this.ThunderTimer = 0;
        this.SparkleTimer = 0;
        Script.Run(this);
    }

    internal static void Run(Script script)
    {
        ActiveScript = script;
        if (Runner is null) {
            Runner = ScriptRun;
            Main.Helper.Events.GameLoop.UpdateTicked += Runner;
        }
    }

    internal static void ScriptRun(object sender, UpdateTickedEventArgs tickedArgs)
    {
        if (!ichortower.TowerCore.Game.IsActive()) {
            return;
        }
        if (ActiveScript is null) {
            Script.Stop();
            return;
        }
        if (ActiveScript.CommandIndex >= ActiveScript.Commands.Length) {
            ActiveScript.OnComplete?.Invoke();
            Script.Stop();
            return;
        }
        if (ActiveScript.Thundering) {
            Script.ThunderRun(ActiveScript);
        }
        if (ActiveScript.ShakeTimer > 0) {
            Game1.viewport.X += Game1.random.Next(-1 * ActiveScript.ShakeIntensity, ActiveScript.ShakeIntensity);
            Game1.viewport.Y += Game1.random.Next(-1 * ActiveScript.ShakeIntensity, ActiveScript.ShakeIntensity);
            ActiveScript.ShakeTimer -= Game1.currentGameTime.ElapsedGameTime.Milliseconds;
        }
        Script.Dispatch(ActiveScript);
    }

    internal static void Stop()
    {
        ActiveScript = null;
        if (Runner is not null) {
            Main.Helper.Events.GameLoop.UpdateTicked -= Runner;
            Runner = null;
        }
    }

    internal static void Dispatch(Script instance)
    {
        string command = instance.Commands[instance.CommandIndex];
        if (command.StartsWith("--")) {
            ++instance.CommandIndex;
            return;
        }
        string[] args = ArgUtility.SplitBySpaceQuoteAware(command);
        MethodInfo[] methods = instance.GetType().GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
        bool ran = false;
        foreach (var m in methods) {
            if (m.Name.EqualsIgnoreCase("Dispatch")) {
                continue;
            }
            if (m.Name.EqualsIgnoreCase(args[0])) {
                m.Invoke(instance, new object[]{args});
                ran = true;
                break;
            }
        }
        if (!ran) {
            Log.Warn($"unknown command '{args[0]}'");
            ++instance.CommandIndex;
        }
    }

    internal void Pause(string[] args)
    {
        if (this.PauseTimer > 0) {
            this.PauseTimer -= Game1.currentGameTime.ElapsedGameTime.Milliseconds;
            if (this.PauseTimer <= 0) {
                this.PauseTimer = 0;
                ++this.CommandIndex;
            }
            return;
        }
        if (!ArgUtility.TryGetInt(args, 1, out int millis, out string error, "int milliseconds")) {
            Log.Error($"bad script: {error}");
            ++this.CommandIndex;
            return;
        }
        if (millis > 0) {
            this.PauseTimer = millis;
        }
        else {
            ++this.CommandIndex;
        }
    }

    internal void PlaySound(string[] args)
    {
        string error;
        if (!ArgUtility.TryGet(args, 1, out string cueName, out error) ||
                !ArgUtility.TryGetOptionalInt(args, 2, out int pitch, out error, defaultValue: 1200)) {
            Log.Error($"bad script: {error}");
            ++this.CommandIndex;
            return;
        }
        this.Location?.playSound(cueName, this.TilePosition, pitch);
        ++this.CommandIndex;
    }

    internal void ScreenShake(string[] args)
    {
        string error;
        if (!ArgUtility.TryGetInt(args, 1, out int intensity, out error) ||
                !ArgUtility.TryGetOptionalInt(args, 2, out int millis, out error, defaultValue: 60000)) {
            Log.Error($"bad script: {error}");
            ++this.CommandIndex;
            return;
        }
        if (intensity == 0) {
            millis = 0;
        }
        this.ShakeTimer = millis;
        this.ShakeIntensity = intensity;
        ++this.CommandIndex;
    }

    // note that for this one w-1g9jan;;,|1(&dbmx;L !!==ERROR== DOES NOT COMP-
    internal void Tas(string[] args)
    {
        string error;
        if (!ArgUtility.TryGetRectangle(args, 1, out Rectangle sourceRect, out error) ||
                !ArgUtility.TryGetFloat(args, 5, out float animationInterval, out error) ||
                !ArgUtility.TryGetInt(args, 6, out int animationFrames, out error) ||
                !ArgUtility.TryGetInt(args, 7, out int loopCount, out error) ||
                !ArgUtility.TryGetVector2(args, 8, out Vector2 coords, out error) ||
                !ArgUtility.TryGetBool(args, 10, out bool flicker, out error) ||
                !ArgUtility.TryGetBool(args, 11, out bool flip, out error) ||
                !ArgUtility.TryGetFloat(args, 12, out float layerDepth, out error) ||
                !ArgUtility.TryGetFloat(args, 13, out float alphaFade, out error) ||
                !ArgUtility.TryGetInt(args, 14, out int scale, out error) ||
                !ArgUtility.TryGetFloat(args, 15, out float scaleChange, out error) ||
                !ArgUtility.TryGetFloat(args, 16, out float rotation, out error) ||
                !ArgUtility.TryGetFloat(args, 17, out float rotationChange, out error)) {
            Log.Error($"bad script: {error}");
            ++this.CommandIndex;
            return;
        }
        TemporaryAnimatedSprite sprite = new(Embedded.Strings["Tas.TextureName"], sourceRect,
                animationInterval, animationFrames, loopCount, (this.TilePosition + coords) * 64f,
                flicker, flip, (this.TilePosition.Y + layerDepth) * 64f / 10000f, alphaFade,
                Color.White, 4 * scale, scaleChange, rotation, rotationChange);
        sprite.extraInfoForEndBehavior = Script.SpecialNumber;
        for (int i = 18; i < args.Length; ++i) {
            switch (args[i]) {
            case "color": {
                if (!ArgUtility.TryGet(args, i+1, out string rawColor, out error)) {
                    Log.Error(error);
                    ++i;
                    break;
                }
                Color? c = Utility.StringToColor(rawColor);
                if (c.HasValue) {
                    sprite.color = c.Value;
                }
                else {
                    Log.Error($"could not parse '{rawColor}' as a color");
                }
                ++i;
                break;
            }
            case "hold_last_frame":
                sprite.holdLastFrame = true;
                break;
            case "ping_pong":
                sprite.pingPong = true;
                break;
            case "motion": {
                if (!ArgUtility.TryGetVector2(args, i+1, out Vector2 motion, out error)) {
                    Log.Error(error);
                    i += 2;
                    break;
                }
                sprite.motion = motion;
                i += 2;
                break;
            }
            case "sound": {
                if (!ArgUtility.TryGet(args, i+1, out string cueName, out error)) {
                    Log.Error(error);
                    ++i;
                    break;
                }
                sprite.startSound = cueName;
                // necessary for the startSound to work. must be delay, not ticks
                sprite.delayBeforeAnimationStart = 1;
                ++i;
                break;
            }
            default:
                Log.Error($"unknown option '{args[i]}'");
                break;
            }
        }
        Game1.Multiplayer.broadcastSprites(this.Location, sprite);
        ++this.CommandIndex;
    }

    internal void FinalTas(string[] args)
    {
        ichortower.MetamorphicCube.Transmuter.TryAddFinalSprite(this.Transmuter, this.Location);
        ++this.CommandIndex;
    }

    // do not use
    internal void ClearTas(string[] args)
    {
        this.Location?.TemporarySprites.RemoveWhere(s => s.extraInfoForEndBehavior == Script.SpecialNumber);
        ++this.CommandIndex;
    }

    internal static void RemoveScriptTas()
    {
        GameLocation loc = ActiveScript?.Location ?? Game1.currentLocation;
        RemoveScriptTas(loc);
    }

    internal static void RemoveScriptTas(GameLocation loc)
    {
        loc?.TemporarySprites.RemoveWhere(s => s.extraInfoForEndBehavior == Script.SpecialNumber);
    }

    internal void ThunderLoop(string[] args)
    {
        if (ArgUtility.TryGet(args, 1, out string stop, out string error) && stop.EqualsIgnoreCase("stop")) {
            this.Thundering = false;
        }
        else {
            this.Thundering = true;
            this.ThunderTimer = 0;
        }
        ++this.CommandIndex;
    }

    internal static void ThunderRun(Script instance)
    {
        instance.ThunderTimer -= Game1.currentGameTime.ElapsedGameTime.Milliseconds;
        if (instance.ThunderTimer <= 0) {
            instance.Location?.playSound(Script.ThunderSound, instance.TilePosition);
            instance.ThunderTimer = 1500;
        }
        instance.SparkleTimer -= Game1.currentGameTime.ElapsedGameTime.Milliseconds;
        if (instance.SparkleTimer <= 0) {
            Vector2 basePos = instance.Transmuter.TileLocation * 64f;
            float dx = (float)Game1.random.NextDouble() * 48f;
            basePos.X += dx;
            basePos.Y -= (float)Game1.random.NextDouble() * 16f;
            Vector2 velocity = new(-1f + dx / 24f, -2f);
            Rectangle sourceRect = new(0, 64, 8, 8);
            TemporaryAnimatedSprite tas = new(Embedded.Strings["Tas.TextureName"], sourceRect,
                    50, 6, 1, basePos, false, false, (instance.TilePosition.Y + 1) * 64f / 10000f,
                    0.03f, Color.White, 4f, 0f, 0f, 0f) {
                motion = velocity,
            };
            Game1.Multiplayer.broadcastSprites(instance.Transmuter.Location, tas);
            instance.SparkleTimer = Game1.random.Next(10, 220);
        }
    }

    internal static float NextEvenFloat(float magnitude)
    {
        float ret = (float)(Game1.random.NextDouble() * (magnitude*2) - magnitude);
        return ret;
    }
}
