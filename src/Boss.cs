using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Netcode;
using StardewValley;
using StardewValley.Audio;
using StardewValley.Monsters;
using StardewValley.Projectiles;
using System;
using System.Collections.Generic;

using ichortower.TowerCore;
using SObject = StardewValley.Object;

namespace ichortower.MetamorphicCube;

internal class Boss : Monster
{
    internal static string InternalName => Embedded.Strings["Monster.Boss.Id"];
    internal static string AttackSound => Embedded.Strings["Monster.Boss.AttackSound"];
    internal static string TeleportSound => Embedded.Strings["Monster.Boss.TeleportSound"];
    internal static string HadoukenSound => Embedded.Strings["Monster.Boss.HadoukenSound"];
    internal static string ShoryukenSound => Embedded.Strings["Monster.Boss.ShoryukenSound"];
    internal static string DieSound => Embedded.Strings["Monster.Boss.DieSound"];
    internal const float Accel = 0.5f;
    internal const float TopSpeed = 5f;
    internal const float MeleeRange = 2f;
    internal const float StandardVDecay = 0.5f;
    internal const float QuickStepVDecay = 0.72f;
    internal const int WalkFrames = 10;
    internal const int TeleportFrames = 2;
    internal const int SpecialMoveCooldownFrames = 320;
    internal static Vector2 RingCenter = new(42f, 20f);

    // durations on these are in ticks, not ms
    internal static List<(int Frame, int Duration)> WalkAnimation = new() {
        (0, WalkFrames), (1, WalkFrames), (2, WalkFrames),
    };
    internal static List<(int Frame, int Duration)> JabAnimation = new() {
        (0, 6), (3, 18),
    };
    internal static List<(int Frame, int Duration)> StrongAnimation = new() {
        (4, 6), (5, 24),
    };
    internal static List<(int Frame, int Duration)> KickAnimation = new() {
        (6, 8), (7, 30),
    };
    internal static List<(int Frame, int Duration)> HadoukenAnimation = new() {
        (8, 10), (9, 42),
    };
    internal static List<(int Frame, int Duration)> ShoryukenAnimation = new() {
        (10, 10), (11, 40), (12, 40),
    };
    internal static List<(int Frame, int Duration)> TeleportOutAnimation = new() {
        (13, TeleportFrames), (14, TeleportFrames), (15, TeleportFrames), (16, 8),
    };
    internal static List<(int Frame, int Duration)> TeleportInAnimation = new() {
        (16, 8), (15, TeleportFrames), (14, TeleportFrames), (13, TeleportFrames),
    };

    internal static int FrameLen(List<(int frame, int dur)> anim)
    {
        int ret = 0;
        foreach (var tuple in anim) {
            ret += tuple.dur;
        }
        return ret;
    }

    internal Vector2 Bearing = Vector2.Zero;
    internal float VDecay = StandardVDecay;
    internal int BaseDamage = 0;

    internal enum BossState {
        Idle,
        Walking,
        JabAttack,
        StrongAttack,
        KickAttack,
        HadoukenAttack,
        ShoryukenAttack,
        TeleportingOut,
        TeleportingIn,
    }

    private BossState _state = BossState.Idle;
    internal BossState State {
        get {
            return _state;
        }
        set {
            AnimIndex = -1;
            _state = value;
        }
    }
    internal NetInt StateTimer = new(0);
    internal int AnimIndex = 0;
    internal int AnimFrameTimer = 0;
    internal Action TeleportAction = null;
    internal int SpecialMoveCooldown = 0;

    public Boss(Vector2 position)
        : base(Boss.InternalName, position)
    {
        // this is just to fix the hp bar
        if (Health > MaxHealth) {
            MaxHealth = Health;
        }
        // for saving/restoring to implement shoryuken's damage
        this.BaseDamage = this.DamageToFarmer;
        this.Sprite.SpriteWidth = 24;
        this.Sprite.SpriteHeight = 32;
        base.forceOneTileWide.Value = true;
        this.Sprite.UpdateSourceRect();
    }

    public override void reloadSprite(bool onlyAppearance = false)
    {
        this.Sprite = new AnimatedSprite("Characters\\Monsters\\" + base.Name);
        this.Sprite.SpriteWidth = 24;
        this.Sprite.SpriteHeight = 32;
        this.Sprite.UpdateSourceRect();
    }

    public override void drawAboveAlwaysFrontLayer(SpriteBatch b)
    {
        base.drawAboveAlwaysFrontLayer(b);
        if (Game1.activeClickableMenu is not null) {
            return;
        }
        int left = Game1.uiViewport.Width / 2 - 768 / 2;
        int top = Game1.uiViewport.Height - 180;
        Rectangle barBounds = new(left, top, (int)(768 * Health / MaxHealth), 24);
        Game1.PushUIMode();
        Utility.drawTextWithShadow(b, displayName, Game1.smallFont, new Vector2(left, top - 40), Color.White);
        b.Draw(Game1.staminaRect, barBounds, new Color(244, 23, 22));
        for (int i = 0; i < 16; ++i) {
            b.Draw(Sprite.Texture,
                   destinationRectangle: new Rectangle(left - 3 + 48 * i, top - 3, 17*3, 10*3),
                   sourceRectangle: new Rectangle(0, 128, 17, 10),
                   color: Color.White);
        }
        Game1.PopUIMode();
    }

    public override int takeDamage(int damage, int xTrajectory, int yTrajectory, bool isBomb,
            double addedPrecision, Farmer who)
    {
        switch (State) {
        case BossState.TeleportingOut:
        case BossState.TeleportingIn:
        case BossState.ShoryukenAttack:
            return 0;
        case BossState.JabAttack:
        case BossState.StrongAttack:
        case BossState.KickAttack:
        case BossState.HadoukenAttack:
            xTrajectory = 0;
            yTrajectory = 0;
            break;
        default:
            break;
        }
        float beforePct = (float)Health / (float)MaxHealth;
        int ret = base.takeDamage(damage, xTrajectory, yTrajectory, isBomb, addedPrecision, who);
        float afterPct = (float)Health / (float)MaxHealth;
        if (beforePct >= 0.5f && afterPct < 0.5f) {
            currentLocation.characters.Add(new Sorcerer(new Vector2(37, 24) * 64f));
            currentLocation.characters.Add(new Sorcerer(new Vector2(46, 24) * 64f));
        }
        return ret;
    }

    public override void behaviorAtGameTick(GameTime time)
    {
        --SpecialMoveCooldown;
        Farmer target = findPlayer();
        PreemptStateMachine(target);
        if (StateTimer.Value > 0) {
            --StateTimer.Value;
            return;
        }
        TimeoutStateMachine(target);
    }

    // state switches that can occur without the state timer expiring
    internal void PreemptStateMachine(Farmer target)
    {
        switch (State) {
        case BossState.Idle:
            if (SpecialMoveCooldown <= 0 && TryUseSpecialMove(target)) {
                break;
            }
            State = BossState.Walking;
            Bearing = (target.Position - Position).SetMagnitude(0.01f);
            break;
        case BossState.Walking:
            if (SpecialMoveCooldown <= 0 && TryUseSpecialMove(target)) {
                break;
            }
            if (InRange(target.Position, MeleeRange)) {
                State = BossState.JabAttack;
                StateTimer.Value = FrameLen(Boss.JabAnimation);
            }
            break;
        default:
            break;
        }
    }

    // state switches that can occur when the state timer expires
    internal void TimeoutStateMachine(Farmer target)
    {
        switch (State) {
        case BossState.JabAttack:
            State = BossState.StrongAttack;
            StateTimer.Value = FrameLen(Boss.StrongAnimation);
            break;
        case BossState.StrongAttack:
            State = BossState.KickAttack;
            StateTimer.Value = FrameLen(Boss.KickAnimation);
            break;
        case BossState.KickAttack:
            State = BossState.Idle;
            StateTimer.Value = 100;
            break;
        case BossState.HadoukenAttack:
        case BossState.ShoryukenAttack:
            DamageToFarmer = BaseDamage;
            State = BossState.Idle;
            StateTimer.Value = 100;
            SpecialMoveCooldown = SpecialMoveCooldownFrames;
            break;
        case BossState.TeleportingOut:
            // generally the action for teleport out is 1. set position 2. set action for in
            State = BossState.TeleportingIn;
            StateTimer.Value = FrameLen(Boss.TeleportInAnimation);
            TeleportAction?.Invoke();
            break;
        case BossState.TeleportingIn:
            // typically not useful since teleports should set state in their action callbacks
            State = BossState.Idle;
            StateTimer.Value = 100;
            TeleportAction?.Invoke();
            break;
        // should never occur since aggro range is big
        case BossState.Idle:
            //faceDirection(Game1.random.Next(0, 4));
            StateTimer.Value = Game1.random.Next(250, 400);
            break;
        default:
            break;
        }
    }

    internal bool TryUseSpecialMove(Farmer target)
    {
        float pct = (float)this.Health / (float)this.MaxHealth;
        float dist = TileRange(target.Position);
        if (dist >= 5f) {
            if (pct < 0.75f && Game1.random.NextDouble() < 0.5) {
                TeleportCloseShoryuken(target);
                return true;
            }
            Hadouken(target);
            return true;
        }
        if (dist < MeleeRange) {
            if (pct < 0.75f && Game1.random.NextDouble() < 0.5) {
                Shoryuken(target);
                return true;
            }
            TeleportFarHadouken(target);
            return true;
        }
        // 1% for each move seems very low but i expect this will be checked often
        double execute = Game1.random.NextDouble();
        if (execute < 0.01) {
            TeleportFarHadouken(target);
            return true;
        }
        else if (execute < 0.02 && pct < 0.75f) {
            TeleportCloseShoryuken(target);
            return true;
        }
        return false;
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
        base.xVelocity *= VDecay;
        if (Math.Abs(base.xVelocity) <= 0.02f) {
            base.xVelocity = 0f;
        }
        base.yVelocity *= VDecay;
        if (Math.Abs(base.yVelocity) <= 0.02f) {
            base.yVelocity = 0f;
        }
        if (base.xVelocity == 0f && base.yVelocity == 0f) {
            VDecay = StandardVDecay;
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
            this.State = BossState.Idle;
            this.StateTimer.Value = Game1.random.Next(30, 60);
        }
    }

    public override void updateMovement(GameLocation location, GameTime time)
    {
        // set bearing/velocity/trajectory as appropriate for state
        switch (this.State) {
        case BossState.Walking:
            Farmer target = this.findPlayer();
            BearTowardPlayer(target);
            AccelerateOneTick();
            break;
        case BossState.TeleportingOut:
        case BossState.TeleportingIn:
        case BossState.HadoukenAttack:
            Bearing = Vector2.Zero;
            setTrajectory(Vector2.Zero);
            break;
        case BossState.ShoryukenAttack:
        case BossState.Idle:
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
        List<(int Frame, int Duration)> whichAnim = this.State switch {
            BossState.Walking => Boss.WalkAnimation,
            BossState.JabAttack => Boss.JabAnimation,
            BossState.StrongAttack => Boss.StrongAnimation,
            BossState.KickAttack => Boss.KickAnimation,
            BossState.HadoukenAttack => Boss.HadoukenAnimation,
            BossState.ShoryukenAttack => Boss.ShoryukenAnimation,
            BossState.TeleportingOut => Boss.TeleportOutAnimation,
            BossState.TeleportingIn => Boss.TeleportInAnimation,
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

        Farmer target = findPlayer();
        // special handlers for particular animation frames
        if (whichAnim == Boss.JabAnimation && AnimIndex == 1) {
            QuickStepIn(target, 0.15f);
            Game1.sounds.PlayAll(AttackSound, currentLocation, this.Tile, 1200, SoundContext.Default);
        }
        if (whichAnim == Boss.StrongAnimation && AnimIndex == 1) {
            QuickStepIn(target, 0.17f);
            Game1.sounds.PlayAll(AttackSound, currentLocation, this.Tile, 1200, SoundContext.Default);
        }
        if (whichAnim == Boss.KickAnimation && AnimIndex == 1) {
            QuickStepIn(target, 0.19f);
            Game1.sounds.PlayAll(AttackSound, currentLocation, this.Tile, 1000, SoundContext.Default);
        }
        if (whichAnim == Boss.HadoukenAnimation && AnimIndex == 1) {
            Point standingPixel = base.StandingPixel;
            Vector2 trajectory = Utility.getVelocityTowardPlayer(standingPixel, 11f, target);
            BasicProjectile hadouken = new(damageToFarmer: 25,
                    spriteIndex: 17, bouncesTillDestruct: 0,
                    tailLength: 3, rotationVelocity: 0f,
                    xVelocity: trajectory.X, yVelocity: trajectory.Y,
                    startingPosition: new Vector2(standingPixel.X - 32, standingPixel.Y),
                    collisionSound: null, bounceSound: null, firingSound: null,
                    explode: true, damagesMonsters: false,
                    base.currentLocation, this);
            hadouken.height.Value = 48f;
            this.currentLocation.projectiles.Add(hadouken);
            Game1.sounds.PlayAll(HadoukenSound, currentLocation, this.Tile, 1200, SoundContext.Default);
        }
        else if (whichAnim == Boss.ShoryukenAnimation && AnimIndex == 1) {
            yJumpVelocity = 20f;
            yJumpOffset = -1;
            yJumpGravity = -0.5f;
            Game1.sounds.PlayAll(ShoryukenSound, currentLocation, this.Tile, 1200, SoundContext.Default);
        }
    }

    protected override void localDeathAnimation()
    {
        Game1.sounds.PlayAll(DieSound, currentLocation, this.Tile, 1200, SoundContext.Default);
        bool flip = this.FacingDirection switch {
            1 or 2 => true,
            _ => false,
        };
        Rectangle sourceRect = new(24, 128, 30, 16);
        Vector2 pos = this.Position;
        Vector2 landing = new((flip ? -96f : 96f) + pos.X, pos.Y + 1f);
        TemporaryAnimatedSprite bounce = new(this.Sprite.textureName.Value,
                sourceRect: sourceRect,
                animationInterval: 2000,
                animationLength: 1,
                numberOfLoops: 1,
                position: pos,
                flicker: false,
                flipped: flip) {
            scale = 4f,
            motion = new Vector2((flip ? -2f : 2f), -6f),
            acceleration = new Vector2(0f, 0.25f),
            xStopCoordinate = (int)landing.X,
            yStopCoordinate = (int)landing.Y,
        };
        TemporaryAnimatedSprite flash = new(this.Sprite.textureName.Value,
                sourceRect: sourceRect,
                animationInterval: 300,
                animationLength: 1,
                numberOfLoops: 6,
                position: landing,
                flicker: true,
                flipped: flip) {
            delayBeforeAnimationStart = 1980,
            scale = 4f,
        };
        Game1.currentLocation.temporarySprites.Add(bounce);
        Game1.currentLocation.temporarySprites.Add(flash);
        flash.endFunction = (int extraInfo) => {
            DelayedAction.functionAfterDelay(() => {
                if (!Game1.player.mailReceived.Add(Embedded.Strings["Reward.MailId"])) {
                    return;
                }
                SObject stardrop = ItemRegistry.Create<SObject>("(O)434");
                stardrop.CanBeSetDown = false;
                stardrop.CanBeGrabbed = false;
                Game1.player.addItemByMenuIfNecessary(stardrop);
            }, 720);
            DelayedAction.functionAfterDelay(OpenReturnPortal, 2200);
            /*
            DelayedAction.functionAfterDelay(() => {
                currentLocation.setMapTile((int)RingCenter.X, (int)RingCenter.Y - 2,
                        125, "Buildings", "outdoors");
                currentLocation.setTileProperty((int)RingCenter.X, (int)RingCenter.Y - 2,
                        "Buildings", "Action", Embedded.Strings["Mod.Id"] + "_WarpHome");
            }, 2000);
            */
        };
    }

    internal void OpenReturnPortal()
    {
        Vector2 position = (RingCenter + new Vector2(-0.5f, -3.5f)) * 64f;
        float layerDepth = (RingCenter.Y - 1f) * 64f / 10000f;

        TemporaryAnimatedSprite crack = new(Embedded.Strings["Tas.TextureName"],
                sourceRect: new Rectangle(0, 80, 32, 48),
                animationInterval: 100,
                animationLength: 6,
                numberOfLoops: 1,
                position: position,
                flicker: false,
                flipped: false) {
            scale = 4f,
            delayBeforeAnimationStart = 1,
            startSound = "coldSpell",
            layerDepth = layerDepth,
        };

        TemporaryAnimatedSprite hold = new(Embedded.Strings["Tas.TextureName"],
                sourceRect: new Rectangle(160, 80, 32, 48),
                animationInterval: 1000,
                animationLength: 1,
                numberOfLoops: 1,
                position: position,
                flicker: false,
                flipped: false) {
            scale = 4f,
            delayBeforeAnimationStart = 580,
            layerDepth = layerDepth,
        };

        TemporaryAnimatedSprite breach = new(Embedded.Strings["Tas.TextureName"],
                sourceRect: new Rectangle(160, 80, 32, 48),
                animationInterval: 120,
                animationLength: 4,
                numberOfLoops: 1,
                position: position,
                flicker: false,
                flipped: false) {
            scale = 4f,
            delayBeforeAnimationStart = 1550,
            startSound = "cursed_mannequin",
            layerDepth = layerDepth + .00001f,
            endFunction = (int extraInfo) => {
                currentLocation.setMapTile((int)RingCenter.X, (int)RingCenter.Y - 2,
                        125, "Buildings", "outdoors");
                currentLocation.setTileProperty((int)RingCenter.X, (int)RingCenter.Y - 2,
                        "Buildings", "Action", Embedded.Strings["Mod.Id"] + "_WarpHome");
            },
        };

        TemporaryAnimatedSprite gate = new(Embedded.Strings["Tas.TextureName"],
                sourceRect: new Rectangle(0, 128, 32, 48),
                animationInterval: 100,
                animationLength: 6,
                numberOfLoops: 9999,
                position: position,
                flicker: false,
                flipped: false) {
            scale = 4f,
            delayBeforeAnimationStart = 2000,
            layerDepth = layerDepth + .00002f,
        };

        currentLocation.temporarySprites.Add(crack);
        currentLocation.temporarySprites.Add(hold);
        currentLocation.temporarySprites.Add(breach);
        currentLocation.temporarySprites.Add(gate);
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

    // presumes closeness
    internal void Shoryuken(Farmer target)
    {
        DamageToFarmer = (DamageToFarmer * 3) / 2;
        QuickStepIn(target, 0.3f);
        State = BossState.ShoryukenAttack;
        StateTimer.Value = FrameLen(ShoryukenAnimation);
    }

    // presumes distance but meh
    internal void Hadouken(Farmer target)
    {
        Vector2 traj = target.Position - Position;
        FaceTarget(target.Position);
        State = BossState.HadoukenAttack;
        StateTimer.Value = FrameLen(HadoukenAnimation);
        // fireball actually handled by animation code lol
    }

    internal void TeleportCloseShoryuken(Farmer target)
    {
        // generally the action for teleport out is 1. set position 2. set action for in
        Vector2 appearLoc = GetClosePosition(target);
        State = BossState.TeleportingOut;
        StateTimer.Value = FrameLen(TeleportOutAnimation);
        TeleportAction = () => {
            Position = appearLoc;
            State = BossState.TeleportingIn;
            StateTimer.Value = FrameLen(TeleportInAnimation);
            TeleportAction = () => {
                Shoryuken(target);
            };
        };
    }

    internal void TeleportFarHadouken(Farmer target)
    {
        Vector2 appearLoc = GetFarPosition(target);
        State = BossState.TeleportingOut;
        StateTimer.Value = FrameLen(TeleportOutAnimation);
        TeleportAction = () => {
            Position = appearLoc;
            State = BossState.TeleportingIn;
            StateTimer.Value = FrameLen(TeleportInAnimation);
            TeleportAction = () => {
                Hadouken(target);
            };
        };
    }

    internal void BearTowardPlayer(Farmer target)
    {
        FaceTarget(target.Position);
        // get angle between our bearing and directly toward target
        Vector2 direct = target.Position - Position;
        float dot = Vector2.Dot(Bearing, direct);
        float magp = Bearing.Magnitude() * direct.Magnitude();
        float angle = MathF.Acos(dot / magp);
        if (double.IsNaN(angle)) {// || angle < 0.02f) {
            return;
        }
        // rotate direct 90 degrees anticlockwise to check for left/right
        float dirdot = Vector2.Dot(Bearing, new Vector2(-1f * direct.Y, direct.X));

        float adj = (float)Math.Min(Math.PI / 30f, Math.Abs(angle));
        float rcos = MathF.Cos(adj);
        float rsin = MathF.Sin(adj);
        if (dirdot > 0f) {
            Bearing = new Vector2(Bearing.X * rcos + Bearing.Y * rsin,
                                  -1f * Bearing.X * rsin + Bearing.Y * rcos);
        }
        else {
            Bearing = new Vector2(Bearing.X * rcos - Bearing.Y * rsin,
                                  Bearing.X * rsin + Bearing.Y * rcos);
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
        mag = Math.Max(mag - Accel, 0f);
        Bearing = Bearing.SetMagnitude(mag);
    }

    internal void QuickStepIn(Farmer target, float power = 0.5f)
    {
        Vector2 closing = (target.Position - Position) * power;
        FaceTarget(target.Position);
        // trajectory y is inverted
        closing.Y *= -1f;
        setTrajectory(closing);
        VDecay = QuickStepVDecay;
    }

    internal void FaceTarget(Vector2 target)
    {
        faceGeneralDirection(target, yBias: 0, opposite: false, useTileCalculations: false);
    }

    internal float TileRange(Vector2 target)
    {
        return Vector2.Distance(target, this.Position) / 64f;
    }

    internal bool InRange(Vector2 target, float tiles)
    {
        return Vector2.Distance(target, this.Position) <= tiles * 64f;
    }

    // get a spot far away from the target farmer, by going opposite them from the ring center
    internal Vector2 GetFarPosition(Farmer target)
    {
        Vector2 dist = RingCenter - target.Tile;
        // just pick a random direction if player is exactly on center point
        if (dist == Vector2.Zero) {
            float rads = (float)(Game1.random.NextDouble() * Math.PI * 2f);
            dist = new(MathF.Cos(rads), MathF.Sin(rads));
        }
        return (RingCenter + dist.SetMagnitude(5f)) * 64f;
    }

    // get a spot close to the farmer (right in front, or right behind)
    internal Vector2 GetClosePosition(Farmer target)
    {
        Vector2[] components = new Vector2[] {
            new(0f, -1f), new(1f, 0f), new(0f, 1f), new(-1f, 0f),
        };
        // appear behind if farmer not moving
        Vector2 dest = target.Tile + components[target.FacingDirection] * -1f;
        if (target.movementDirections.Count == 0) {
            return EjectFromRingWall(dest) * 64f;
        }
        // appear ahead if farmer moving
        Vector2 farmerMovement = Vector2.Zero;
        foreach (int d in target.movementDirections) {
            farmerMovement += components[d];
        }
        dest = target.Tile + farmerMovement.SetMagnitude(1f);
        return EjectFromRingWall(dest) * 64f;
    }

    // make sure this position is safe by shrinking it to fit within the ring
    internal Vector2 EjectFromRingWall(Vector2 pos)
    {
        Vector2 mag = pos - RingCenter;
        if (mag.Magnitude() <= 6f) {
            return pos;
        }
        mag.SetMagnitude(5.5f);
        return RingCenter + mag;
    }
}
