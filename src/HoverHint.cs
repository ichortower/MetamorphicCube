using ichortower.TowerCore;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.Menus;
using System.Linq;

using Enc = System.Text.Encoding;

namespace ichortower.MetamorphicCube;

internal class HoverHint
{
    internal const int FramesPerCheck = 10;
    internal const double GlitchChance = 0.001;
    internal const float StartRate = 0.5f;
    internal const float HoldRate = 0.99f;
    internal const float EndRate = 0.1f;
    internal const int PhaseInTime = 2000;
    internal const int PhaseHoldTime = 6000;
    internal const int PhaseOutTime = 2000;
    internal const string Sound = "woodyHit";

    internal static int TickCount = 0;
    internal static float PhaseTimer = -1f;

    internal static string Zalgofy(string text)
    {
        if (text is null) {
            return null;
        }
        return Enc.UTF8.GetString(Embedded.Unlock(Enc.UTF8.GetBytes(text)));
    }

    internal static string CachedItemId = null;
    internal static string CachedTitle = null;
    internal static string CachedText = null;
    internal static bool LastPhaseState = false;


    [TargetMethod(typeof(IClickableMenu), nameof(IClickableMenu.drawToolTip))]
    [PatchType(PatchTypes.Prefix)]
    public static void IClickableMenu_drawToolTip_Prefix(IClickableMenu __instance,
            ref string hoverText, ref string hoverTitle, Item hoveredItem)
    {
        Recipe r = Embedded.TheRecipe;
        bool valid = r.InputItems.Any(i => i.ContainsIgnoreCase(hoveredItem.QualifiedItemId));
        if (!valid) {
            return;
        }
        if (!hoveredItem.QualifiedItemId.EqualsIgnoreCase(CachedItemId)) {
            CachedTitle = Zalgofy(hoverTitle);
            CachedText = Zalgofy(hoverText);
        }
        if (PhaseTimer >= 0f) {
            bool phase = PhaseCheck(PhaseTimer);
            if (LastPhaseState != phase) {
                Game1.playSound(Sound);
            }
            LastPhaseState = phase;
            if (phase) {
                hoverTitle = CachedTitle;
                hoverText = CachedText;
            }
            if (PhaseTimer >= 0f) {
                PhaseTimer += (float)Game1.currentGameTime.ElapsedGameTime.TotalMilliseconds;
            }
            return;
        }

        TickCount = (TickCount + 1) % FramesPerCheck;
        if (TickCount != 0) {
            return;
        }
        if (Game1.random.NextDouble() > GlitchChance) {
            return;
        }
        // setting this to >= 0f triggers the phase to start
        PhaseTimer = 0f;
    }

    // return true if text should be zalgo instead of default this frame.
    // side effect: end phasing (set timer to -1) when time is up
    internal static bool PhaseCheck(float time)
    {
        float one = PhaseInTime;
        float two = PhaseInTime + PhaseHoldTime;
        float three = PhaseInTime + PhaseHoldTime + PhaseOutTime;
        float chance = 0f;
        if (time < 0f || time > three) {
            PhaseTimer = -1f;
            return false;
        }

        if (time >= 0f && time <= one) {
            chance = Utility.Lerp(StartRate, HoldRate, time / one);
        }
        else if (time > one && time <= two) {
            chance = HoldRate;
        }
        else if (time > two && time <= three) {
            chance = Utility.Lerp(HoldRate, EndRate, (time - two) / (three - two));
        }
        return Game1.random.NextDouble() < chance;
    }
}
