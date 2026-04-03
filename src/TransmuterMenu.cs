using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Inventories;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Menus;
using SObject = StardewValley.Object;

using ichortower.TowerCore;

namespace ichortower.MetamorphicCube;

internal class TransmuterMenu : MenuWithInventory
{
    public InventoryMenu contents;
    public ClickableTextureComponent goButton;
    public SObject transmuter;
    public int shakeTimer = 0;

    public TransmuterMenu(SObject context)
        : base(null, okButton: true, trashCan: false)
    {
        this.transmuter = context;
        Inventory inv = Transmuter.GetInventory(context, Transmuter.MainStorageKey);
        Point pos = GetContentsPosition();
        this.contents = new InventoryMenu(pos.X, pos.Y, playerInventory: false,
                actualInventory: inv, capacity: Transmuter.Capacity);
        this.inventory.showGrayedOutSlots = true;

        Rectangle bounds = new(contents.xPositionOnScreen + contents.width + 64,
                contents.yPositionOnScreen + 132, 64, 64);
        ParsedItemData itemData = ItemRegistry.GetDataOrErrorItem(context.QualifiedItemId);
        this.goButton = new ClickableTextureComponent(bounds, itemData.GetTexture(),
                new Rectangle(0, 14, 16, 16), 4f) {
            hoverText = "Transmute",
        };
    }

    internal Point GetContentsPosition()
    {
        int width = 64 * 3;
        return new Point(Game1.uiViewport.Width / 2 - width / 2 - 20,
                         this.yPositionOnScreen + 32);
    }

    public override void draw(SpriteBatch b)
    {
        if (shakeTimer > 0) {
            --shakeTimer;
            contents.xPositionOnScreen = GetContentsPosition().X + shakeTimer switch {
                0 => 0,
                _ => -2 + 4 * (shakeTimer % 2),
            };
        }
        base.draw(b, drawUpperPortion: false, drawDescriptionArea: false);
        Game1.drawDialogueBox(
                this.contents.xPositionOnScreen - IClickableMenu.borderWidth - IClickableMenu.spaceToClearSideBorder,
                this.contents.yPositionOnScreen - IClickableMenu.borderWidth - IClickableMenu.spaceToClearTopBorder,
                this.contents.width + IClickableMenu.borderWidth * 2 + IClickableMenu.spaceToClearSideBorder * 2,
                this.contents.height + IClickableMenu.spaceToClearTopBorder + IClickableMenu.borderWidth * 2,
                speaker: false, drawOnlyBox: true);
        this.contents.draw(b);
        this.goButton?.draw(b);
        if (base.hoverText is not null && base.hoveredItem is null) {
            IClickableMenu.drawHoverText(b, base.hoverText, Game1.smallFont);
        }
        else if (base.hoveredItem is not null) {
            IClickableMenu.drawToolTip(b, base.hoveredItem.getDescription(), base.hoveredItem.DisplayName,
                    base.hoveredItem, base.heldItem != null);
        }
        base.drawMouse(b);
    }

    public override void performHoverAction(int x, int y)
    {
        base.hoverText = null;
        base.hoveredItem = null;
        base.performHoverAction(x, y);
        this.goButton?.tryHover(x, y, 0.25f);
        if (this.goButton?.containsPoint(x, y) is true) {
            base.hoverText = this.goButton.hoverText;
        }
        Item hover = this.contents.hover(x, y, base.heldItem);
        if (hover is not null) {
            base.hoveredItem = hover;
        }
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.goButton?.containsPoint(x, y) is true) {
            bool success =  Transmuter.TryStartTransmutation(transmuter, out bool shouldCloseMenu);
            if (!success) {
                Game1.playSound(Transmuter.ErrorSound);
                shakeTimer = 12;
            }
            if (shouldCloseMenu) {
                base.exitThisMenu(playSound: false);
            }
            return;
        }
        base.receiveLeftClick(x, y, playSound: false);
        bool fromPlayer = true;
        if (base.heldItem is null) {
            base.heldItem = this.contents.leftClick(x, y, base.heldItem, playSound: false);
            if (base.heldItem is not null) {
                fromPlayer = false;
            }
        }
        this.TryGrabItem(fromPlayer);
    }

    public override void receiveRightClick(int x, int y, bool playSound = true)
    {
        base.receiveRightClick(x, y, playSound: false);
        bool fromPlayer = true;
        if (base.heldItem is null) {
            base.heldItem = this.contents.rightClick(x, y, base.heldItem, playSound: false);
            if (base.heldItem is not null) {
                fromPlayer = false;
            }
        }
        this.TryGrabItem(fromPlayer);
    }

    internal void TryGrabItem(bool fromPlayer)
    {
        if (base.heldItem is null) {
            return;
        }
        if (fromPlayer) {
            this.GrabItemFromPlayer(base.heldItem, Game1.player);
        }
        else {
            this.GrabItemFromContents(base.heldItem, Game1.player);
        }
        base.heldItem = null;
    }

    internal void GrabItemFromContents(Item item, Farmer who)
    {
        Inventory inv = Transmuter.GetInventory(transmuter, Transmuter.MainStorageKey);
        if (who.couldInventoryAcceptThisItem(item)) {
            this.inventory.tryToAddItem(item, Transmuter.WithdrawItemSound);
            inv.RemoveEmptySlots();
        }
        else {
            inv.AddWithStacking(item);
        }
    }

    internal void GrabItemFromPlayer(Item item, Farmer who)
    {
        if (item.Stack == 0) {
            item.Stack = 1;
        }
        Inventory inv = Transmuter.GetInventory(transmuter, Transmuter.MainStorageKey);
        Item tmp = inv.AddWithStacking(item);
        if (tmp is null) {
            who.removeItemFromInventory(item);
            Game1.playSound(Transmuter.DepositItemSound, out ICue cue);
            cue.Pitch = 1800f / 2400f;
        }
        else {
            tmp = who.addItemToInventory(tmp);
        }
    }
}
