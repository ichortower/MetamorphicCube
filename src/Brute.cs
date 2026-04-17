using Microsoft.Xna.Framework;
using Netcode;
using StardewValley;
using StardewValley.Audio;
using StardewValley.Monsters;
using System;
using System.Collections.Generic;
using System.Linq;
using ichortower.TowerCore;

namespace ichortower.MetamorphicCube;

internal class Brute : Monster
{
    internal static string InternalName => Embedded.Strings["Monster.Brute.Id"];
    internal static string CryCue => Embedded.Strings["Monster.Brute.ChargeSound"];
    internal static string BonkCue => Embedded.Strings["Monster.Brute.BonkSound"];
    internal static string DieCue => Embedded.Strings["Monster.Brute.DieSound"];
    internal static string SpecialStatusKey => Embedded.Strings["Monster.SpecialStatus.Key"];
    internal static string SpecialStatusSound => Embedded.Strings["Monster.SpecialStatus.DefeatSound"];
    internal static float Accel = 0.42f;
    internal static float TopSpeed = 12f;
    internal const int RunFrames = 8;
    internal const int StunFrames = 8;

    // durations on these are in ticks, not ms
    internal static List<(int Frame, int Duration)> CrouchAnimation = new() {
        (1, 40),
    };
    internal static List<(int Frame, int Duration)> RunAnimation = new() {
        (2, RunFrames), (3, RunFrames), (4, RunFrames), (3, RunFrames),
    };
    internal static List<(int Frame, int Duration)> StunAnimation = new() {
        (5, StunFrames), (6, StunFrames), (7, StunFrames),
    };

    internal const int MaxPoolSize = 10;
    internal static List<ICue> CryPool = new();
    internal static List<ICue> BonkPool = new();
    internal static List<ICue> DiePool = new();

    internal Vector2 Bearing = Vector2.Zero;

    internal enum BruteState {
        Idle,
        GearingUp,
        Charging,
        Stopping,
        Stunned,
    }

    internal BruteState State = BruteState.Idle;
    internal NetInt StateTimer = new(0);
    internal int AnimIndex = 0;
    internal int AnimFrameTimer = 0;
    internal int VoicePitch = 1200;

    public Brute()
        : this(Vector2.Zero)
    {
    }

    public Brute(Vector2 position)
        : base(Brute.InternalName, position)
    {
        this.Sprite.SpriteHeight = 24;
        this.Sprite.SpriteWidth = 24;
        base.forceOneTileWide.Value = true;
        this.Sprite.UpdateSourceRect();
        this.VoicePitch = 400 + 100 * Game1.random.Next(7);
    }

    public override void reloadSprite(bool onlyAppearance = false)
    {
        this.Sprite = new AnimatedSprite("Characters\\Monsters\\" + base.Name);
        this.Sprite.SpriteHeight = 24;
        this.Sprite.SpriteWidth = 24;
        this.Sprite.UpdateSourceRect();
    }

    public override int takeDamage(int damage, int xTrajectory, int yTrajectory, bool isBomb,
            double addedPrecision, Farmer who)
    {
        // if stunned: increase damage, remove knockback
        if (this.State == BruteState.Stunned) {
            damage = damage * 5 / 4;
            xTrajectory = 0;
            yTrajectory = 0;
        }
        return base.takeDamage(damage, xTrajectory, yTrajectory, isBomb, addedPrecision, who);
    }

    public override void behaviorAtGameTick(GameTime time)
    {
        Farmer target = this.findPlayer();
        if (this.State == BruteState.Idle && Vector2.Distance(target.Position, this.Position) <=
                this.moveTowardPlayerThreshold.Value * 64f) {
            this.State = BruteState.GearingUp;
            this.StateTimer.Value = Game1.random.Next(30, 40);
            SoundPool(CryCue, CryPool, this.VoicePitch);
            base.faceGeneralDirection(target.getStandingPosition());
            return;
        }

        if (this.StateTimer.Value > 0) {
            --this.StateTimer.Value;
            return;
        }

        // our current state has expired so it is time to choose a new one

        switch (this.State) {
        case BruteState.Charging:
            this.State = BruteState.Stopping;
            this.StateTimer.Value = Game1.random.Next(40, 100);
            break;
        case BruteState.GearingUp:
            this.State = BruteState.Charging;
            // in practice, charges don't last this long
            this.StateTimer.Value = 300;
            base.faceGeneralDirection(target.getStandingPosition());
            break;
        case BruteState.Idle:
            this.faceDirection(Game1.random.Next(0, 4));
            this.StateTimer.Value = Game1.random.Next(250, 400);
            break;
        case BruteState.Stopping:
        case BruteState.Stunned:
        default:
            this.State = BruteState.Idle;
            this.StateTimer.Value = Game1.random.Next(250, 400);
            break;
        }
    }

    public override void MovePosition(GameTime time, xTile.Dimensions.Rectangle viewport,
            GameLocation currentLocation)
    {
        if (this.stunTime.Value > 0) {
            return;
        }
        Vector2 fromBearing = this.State switch {
            BruteState.GearingUp => Vector2.Zero,
            _ => this.Bearing,
        };
        // REMINDER: character.yVelocity is inverted (positive means y coord decreases)
        Vector2 fromVelocity = new(base.xVelocity, -1f * base.yVelocity);
        base.xVelocity /= 2f;
        if (Math.Abs(base.xVelocity) <= 0.05f) {
            base.xVelocity = 0f;
        }
        base.yVelocity /= 2f;
        if (Math.Abs(base.yVelocity) <= 0.05f) {
            base.yVelocity = 0f;
        }
        if (double.IsNaN(fromVelocity.X) || double.IsNaN(fromVelocity.Y)) {
            fromVelocity = Vector2.Zero;
        }

        Vector2 netMovement = fromBearing + fromVelocity;

        if (netMovement == Vector2.Zero) {
            return;
        }

        base.lastPosition = base.Position;
        Rectangle checkPosition = this.GetBoundingBox();
        Vector2 start = new(checkPosition.X, checkPosition.Y);
        Vector2 end = start + netMovement;
        int steps = 4;
        bool collision = false;
        bool isGlider = false;
        for (int i = 0; i < steps; ++i) {
            checkPosition.X = (int)Utility.Lerp(start.X, end.X, (float)(i+1) / (float)steps);
            checkPosition.Y = (int)Utility.Lerp(start.Y, end.Y, (float)(i+1) / (float)steps);
            if (this.currentLocation?.isCollidingPosition(checkPosition,
                    viewport, isFarmer: false, this.DamageToFarmer, isGlider, this) is true) {
                collision = true;
                break;
            }
            base.Position += netMovement / (float)steps;
        }

        if (!collision) {
            return;
        }
        this.Bearing = Vector2.Zero;
        if (netMovement.Magnitude() > (Brute.TopSpeed / 2f)) {
            // reminder y trajectory is inverted
            this.setTrajectory(new Vector2(-2.5f * netMovement.X, 2.5f * netMovement.Y));
            this.State = BruteState.Stunned;
            this.StateTimer.Value = 180;
            int pitch = 800 + 200 * Game1.random.Next(5);
            SoundPool(BonkCue, BonkPool, pitch);
        }
        else {
            this.State = BruteState.Stopping;
            this.StateTimer.Value = Game1.random.Next(40, 100);
        }
    }

    public override void updateMovement(GameLocation location, GameTime time)
    {
        switch (this.State) {
        case BruteState.Stunned:
            this.Bearing = Vector2.Zero;
            break;
        case BruteState.Stopping:
            DecelerateOneTick();
            break;
        case BruteState.Charging:
            AccelerateOneTick();
            StopIfMissed();
            // TurnSlightlyTowardPlayer();
            break;
        case BruteState.GearingUp:
            this.Bearing = this.GetPlayerVector().SetMagnitude(0.01f);
            break;
        case BruteState.Idle:
        default:
            break;
        }
        MovePosition(time, Game1.viewport, location);
    }

    protected override void updateAnimation(GameTime time)
    {
        if (this.AnimFrameTimer > 0) {
            --this.AnimFrameTimer;
            return;
        }
        EnsureSpecialMarker();
        List<(int Frame, int Duration)> whichAnim = this.State switch {
            BruteState.Stunned => Brute.StunAnimation,
            BruteState.GearingUp => Brute.CrouchAnimation,
            BruteState.Charging => Brute.RunAnimation,
            _ => null,
        };
        if (whichAnim is null) {
            this.AnimIndex = -1;
            SetDirectionFrame(0);
            return;
        }
        ++this.AnimIndex;
        AnimIndex %= whichAnim.Count;
        SetDirectionFrame(whichAnim[AnimIndex].Frame);
        this.AnimFrameTimer = whichAnim[AnimIndex].Duration;
    }

    protected override void localDeathAnimation()
    {
        SoundPool(DieCue, DiePool, this.VoicePitch);
        if (!this.IsSpecial()) {
            return;
        }
        this.currentLocation.playSound(SpecialStatusSound);
        DestroySpecialMarker();
        this.currentLocation.TryOpenInteriorGate();
    }
    

    // set sprite's frame to given index in appropriate row for facing direction
    internal void SetDirectionFrame(int index)
    {
        int framesPerRow = (this.Sprite.Texture?.Width ?? 120) / this.Sprite.SpriteWidth;
        int firstInRow = framesPerRow * this.FacingDirection switch {
            0 => 2,
            2 => 0,
            _ => this.FacingDirection,
        };
        this.Sprite.CurrentFrame = firstInRow + index;
        this.Sprite.UpdateSourceRect();
    }

    internal Vector2 GetPlayerVector()
    {
        return this.findPlayer().Position - this.Position;
    }

    // assumes brute is charging. if player out of scope (angle > pi/3), stop
    internal void StopIfMissed()
    {
        float dot = Vector2.Dot(this.Bearing, this.GetPlayerVector());
        float magp = this.Bearing.Magnitude() * this.GetPlayerVector().Magnitude();
        float angle = MathF.Acos(dot / magp);
        if (angle > (Math.PI / 3f)) {
            this.StateTimer.Value = 0;
        }
    }

    internal void AccelerateOneTick()
    {
        float mag = Bearing.Magnitude();
        mag = Math.Min(mag + Accel, TopSpeed);
        Bearing = Bearing.SetMagnitude(mag);
    }

    internal void DecelerateOneTick()
    {
        if (Bearing == Vector2.Zero) {
            return;
        }
        float mag = Bearing.Magnitude();
        mag = Math.Max(mag - Accel*0.7f, 0f);
        Bearing = Bearing.SetMagnitude(mag);
    }

    internal void MakeSpecial()
    {
        this.modData[SpecialStatusKey] = "true";
    }

    internal bool IsSpecial()
    {
        return this.modData.ContainsKey(SpecialStatusKey);
    }

    internal void EnsureSpecialMarker()
    {
        if (!this.IsSpecial()) {
            return;
        }
        bool exists = this.currentLocation.temporarySprites.Any(tas => {
            return tas.textureName == "LooseSprites\\Cursors" &&
                    System.Object.ReferenceEquals(this, tas.attachedCharacter);
        });
        if (exists) {
            return;
        }
        TemporaryAnimatedSprite tas = new(textureName: "LooseSprites\\Cursors",
                sourceRect: new Rectangle(346, 400, 8, 8),
                animationInterval: 1000f,
                animationLength: 1,
                numberOfLoops: 9999,
                position: new Vector2(16f, -84f),
                flicker: false, flipped: false) {
            scale = 4f,
            layerDepth = 1f,
            attachedCharacter = this,
            positionFollowsAttachedCharacter = true,
        };
        Game1.Multiplayer.broadcastSprites(currentLocation, tas);
    }

    internal void DestroySpecialMarker()
    {
        List<TemporaryAnimatedSprite> toRemove = currentLocation.temporarySprites.Where(tas => {
            return tas.textureName == "LooseSprites\\Cursors" &&
                    System.Object.ReferenceEquals(this, tas.attachedCharacter);
        }).ToList();
        foreach (var t in toRemove) {
            currentLocation.temporarySprites.Remove(t);
        }
    }

    internal void SoundPool(string cueName, List<ICue> pool, int pitch = 1200)
    {
        if (!Game1.IsMasterGame) {
            return;
        }
        pool.RemoveAll(cue => !cue.IsPlaying);
        if (pool.Count >= MaxPoolSize) {
            return;
        }
        // FIXME need playall for mp but that doesn't have an out cue version
        if (Game1.sounds.PlayLocal(cueName, this.currentLocation, this.Tile, pitch,
                SoundContext.Default, out ICue cue)) {
            cue.Pitch = pitch / 2400f;
            pool.Add(cue);
        }
    }

}
