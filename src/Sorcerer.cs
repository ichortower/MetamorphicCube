using Microsoft.Xna.Framework;
using Netcode;
using StardewValley;
using StardewValley.Audio;
using StardewValley.Extensions;
using StardewValley.Monsters;
using StardewValley.Projectiles;
using System;
using System.Collections.Generic;
using System.Linq;

using ichortower.TowerCore;

namespace ichortower.MetamorphicCube;

internal class Sorcerer : Monster
{
    internal static string InternalName => Embedded.Strings["Monster.Sorcerer.Id"];
    internal static string CastCue => Embedded.Strings["Monster.Sorcerer.CastSound"];
    internal static string FireballCue => Embedded.Strings["Monster.Sorcerer.FireballSound"];
    internal static string ShieldCue => Embedded.Strings["Monster.Sorcerer.ShieldSound"];
    internal static string DieCue => Embedded.Strings["Monster.Sorcerer.DieSound"];
    internal static string SpecialStatusKey => Embedded.Strings["Monster.SpecialStatus.Key"];
    internal static string SpecialStatusSound => Embedded.Strings["Monster.SpecialStatus.DefeatSound"];
    internal const float Accel = 0.31f;
    internal const float TopSpeed = 3f;
    internal const int WalkFrames = 10;
    internal const int CastFrames = 4;
    internal const int FireballCooldownFrames = 150;
    internal const float FireballRange = 9f;
    internal const int ShieldCooldownFrames = 450;
    internal const float ShieldRange = 5f;
    internal const float ShieldSelfPanicDistance = 4f;
    internal const float MoveAwayFromPlayerRange = 5f;
    internal static Color ShieldGlowColor = new(149, 236, 242);

    // durations on these are in ticks, not ms
    internal static List<(int Frame, int Duration)> WalkAnimation = new() {
        (0, WalkFrames), (1, WalkFrames), (0, WalkFrames), (2, WalkFrames),
    };
    internal static List<(int Frame, int Duration)> CastAnimation = new() {
        (3, CastFrames), (4, CastFrames),
    };
    internal static List<(int Frame, int Duration)> CooldownAnimation = new() {
        (5, 40),
    };

    internal const int MaxPoolSize = 10;
    internal static List<ICue> CastPool = new();
    internal static List<ICue> FireballPool = new();
    internal static List<ICue> ShieldPool = new();
    internal static List<ICue> DiePool = new();

    internal Vector2 Bearing = Vector2.Zero;

    internal enum SorcererState {
        Inactive,
        Idle,
        Walking,
        Casting,
        Cooldown,
    }

    internal SorcererState State = SorcererState.Inactive;
    internal NetInt StateTimer = new(0);
    internal int AnimIndex = 0;
    internal int AnimFrameTimer = 0;
    internal int FireballCooldown = 0;
    internal int ShieldCooldown = 0;
    internal Action SpellCastAction = null;


    public Sorcerer(Vector2 position)
        : base(Sorcerer.InternalName, position)
    {
        this.Sprite.SpriteHeight = 24;
        this.Sprite.SpriteWidth = 24;
        base.forceOneTileWide.Value = true;
        this.Sprite.UpdateSourceRect();
    }

    public override void reloadSprite(bool onlyAppearance = false)
    {
        this.Sprite = new AnimatedSprite("Characters\\Monsters\\" + base.Name);
        this.Sprite.SpriteHeight = 24;
        this.Sprite.SpriteWidth = 24;
        this.Sprite.UpdateSourceRect();
    }

    public override void behaviorAtGameTick(GameTime time)
    {
        --this.ShieldCooldown;
        --this.FireballCooldown;
        Farmer target = this.findPlayer();

        if (State == SorcererState.Idle) {
            Farmer[] threats = currentLocation.farmers.Where((f) => {
                return InRange(f.Position, MoveAwayFromPlayerRange);
            }).ToArray();
            Vector2 goal = Vector2.Zero;
            foreach (Farmer f in threats) {
                goal += (this.Position - f.Position);
            }
            if (goal != Vector2.Zero) {
                State = SorcererState.Walking;
                faceGeneralDirection(Position + goal);
                Bearing = goal.SetMagnitude(0.01f);
                StateTimer.Value = 120;
                return;
            }
            // if sheng long is on the map and not in shield range, move closer
            Monster theguy = FindBoss();
            if (theguy is not null && !InRange(theguy.Position, ShieldRange)) {
                goal = theguy.Position - Position;
                State = SorcererState.Walking;
                faceGeneralDirection(theguy.Position);
                Bearing = goal.SetMagnitude(0.01f);
                StateTimer.Value = 120;
            }
        }

        if (this.State == SorcererState.Idle || this.State == SorcererState.Walking) {
            if (this.ShieldCooldown <= 0) {
                Monster theguy = FindBoss();
                if (theguy is not null && InRange(theguy.Position, ShieldRange)) {
                    SpellCastAction = delegate {
                        ShieldTarget(theguy);
                    };
                    State = SorcererState.Casting;
                    SoundPool(CastCue, CastPool);
                    StateTimer.Value = 90;
                    return;
                }
            }
            if (this.FireballCooldown <= 0 && this.InRange(target.Position, Sorcerer.FireballRange)) {
                this.SpellCastAction = this.FireballNearestPlayer;
                this.faceGeneralDirection(target.getStandingPosition());
                this.State = SorcererState.Casting;
                SoundPool(CastCue, CastPool);
                this.StateTimer.Value = 60;
                return;
            }
            if (this.ShieldCooldown <= 0) {
                if (this.InRange(target.Position, Sorcerer.ShieldSelfPanicDistance)) {
                    this.SpellCastAction = this.ShieldSelf;
                    this.State = SorcererState.Casting;
                    SoundPool(CastCue, CastPool);
                    this.StateTimer.Value = 90;
                    return;
                }
                Monster[] allies = this.currentLocation.characters.Where((c) => {
                    if (System.Object.ReferenceEquals(c, this)) {
                        return false;
                    }
                    if (c is not Brute && c is not Sorcerer) {
                        return false;
                    }
                    return this.InRange(c.Position, Sorcerer.ShieldRange);
                }).Select(c => c as Monster).ToArray();
                if (allies.Length > 0) {
                    this.SpellCastAction = delegate {
                        this.ShieldTarget(Game1.random.ChooseFrom(allies));
                    };
                    this.State = SorcererState.Casting;
                    SoundPool(CastCue, CastPool);
                    this.StateTimer.Value = 90;
                    return;
                }
            }
            if (InRange(target.Position, this.moveTowardPlayerThreshold.Value * 1.25f) &&
                    !InRange(target.Position, FireballRange)) {
                // order is a bit unusual here because we don't want to reset speed to 0.01
                // if we're already walking
                faceGeneralDirection(target.Position);
                Bearing = (target.Position - this.Position);
                if (this.State == SorcererState.Idle) {
                    Bearing = Bearing.SetMagnitude(0.01f);
                }
                State = SorcererState.Walking;
                StateTimer.Value = 30;
                return;
            }

        }

        if (this.StateTimer.Value > 0) {
            --this.StateTimer.Value;
            return;
        }

        switch (this.State) {
        case SorcererState.Casting:
            this.SpellCastAction();
            this.State = SorcererState.Cooldown;
            this.StateTimer.Value = 40;
            break;
        case SorcererState.Inactive:
            if (this.InRange(target.Position, this.moveTowardPlayerThreshold.Value)) {
                this.State = SorcererState.Idle;
                this.StateTimer.Value = Game1.random.Next(250, 400);
            }
            break;
        case SorcererState.Idle:
            if (!this.InRange(target.Position, this.moveTowardPlayerThreshold.Value * 1.5f)) {
                this.State = SorcererState.Inactive;
                break;
            }
            this.faceDirection(Game1.random.Next(0, 4));
            this.StateTimer.Value = Game1.random.Next(250, 400);
            break;
        case SorcererState.Walking:
        case SorcererState.Cooldown:
        default:
            this.State = SorcererState.Idle;
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
        Vector2 fromBearing = this.Bearing;
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

        if (collision) {
            this.State = SorcererState.Idle;
            this.StateTimer.Value = Game1.random.Next(250, 400);
        }
    }

    public override void updateMovement(GameLocation location, GameTime time)
    {
        switch (this.State) {
        case SorcererState.Walking:
            AccelerateOneTick();
            break;
        case SorcererState.Casting:
        case SorcererState.Cooldown:
        case SorcererState.Idle:
        default:
            DecelerateOneTick();
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
            SorcererState.Walking => Sorcerer.WalkAnimation,
            SorcererState.Casting => Sorcerer.CastAnimation,
            SorcererState.Cooldown => Sorcerer.CooldownAnimation,
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
        SoundPool(DieCue, DiePool);
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
        mag = Math.Max(mag - Accel, 0f);
        Bearing = Bearing.SetMagnitude(mag);
    }

    internal Monster FindBoss()
    {
        return currentLocation.characters.Where(c => c is Boss)
                .Select(c => c as Monster).FirstOrDefault(null as Monster);
    }

    internal bool InRange(Vector2 target, float tiles)
    {
        return Vector2.Distance(target, this.Position) <= tiles * 64f;
    }

    // this does not check spell range. that was checked at cast time and it can follow
    // out of initial range once casting has begun
    internal void FireballNearestPlayer()
    {
        Farmer target = this.findPlayer();
        Point standingPixel = base.StandingPixel;
        Vector2 trajectory = Utility.getVelocityTowardPlayer(standingPixel, 8f, target);
        BasicProjectile fireball = new(damageToFarmer: 20,
                spriteIndex: 10, bouncesTillDestruct: 1,
                tailLength: 4, rotationVelocity: 0f,
                xVelocity: trajectory.X, yVelocity: trajectory.Y,
                startingPosition: new Vector2(standingPixel.X - 32, standingPixel.Y),
                collisionSound: null, bounceSound: null, firingSound: null,
                explode: true, damagesMonsters: false,
                base.currentLocation, this);
        fireball.height.Value = 48f;
        this.currentLocation.projectiles.Add(fireball);
        SoundPool(FireballCue, FireballPool);
        this.faceGeneralDirection(target.getStandingPosition());
        this.FireballCooldown = Sorcerer.FireballCooldownFrames;
    }

    internal void ShieldSelf()
    {
        ShieldTarget(this);
    }

    internal void ShieldTarget(Monster m)
    {
        // built-in monster invincible time is in ms
        // don't call func, it sets glowing
        m.invincibleCountdown = 4000;

        TemporaryAnimatedSprite tas = new(textureName: "Characters\\Monsters\\" + base.Name,
                sourceRect: new Rectangle(24, 96, 24, 24),
                animationInterval: 100f,
                animationLength: 2,
                numberOfLoops: 20,
                position: new Vector2(-16f, -56f), // this is an offset when following character
                flicker: false, flipped: false) {
            scale = 4f,
            layerDepth = 1f,
            attachedCharacter = m,
            positionFollowsAttachedCharacter = true,
        };
        Game1.Multiplayer.broadcastSprites(m.currentLocation, tas);

        SoundPool(ShieldCue, ShieldPool);
        this.faceGeneralDirection(m.getStandingPosition());
        this.ShieldCooldown = Sorcerer.ShieldCooldownFrames;
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
