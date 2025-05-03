using System;
using ValkyrNutritions.Behaviors;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace ValkyrNutritions;

public class ValkyrHud : HudElement
{
    private GuiElementStatbar staminaBar;

    public ValkyrHud(ICoreClientAPI capi) : base(capi)
    {
        capi.Event.RegisterGameTickListener(new Action<float>(this.onGameTick), 20, 100);
    }

    public void onGameTick(float dt)
    {
        this.compose();
        this.updateStamina();
    }

    public override void OnOwnPlayerDataReceived()
    {
        this.compose();
        this.updateStamina();
    }

    private void updateStamina()
    {
        var player = capi.World.Player.Entity;
        ITreeAttribute staminaTree = player.WatchedAttributes.GetTreeAttribute(StaminaSystem.N.Attr);

        if (staminaTree == null)
        {
            return;
        }
        var newStamina = staminaTree.GetFloat(StaminaSystem.N.Current, 0f);
        var newMaxStamina = player.Stats.GetBlended(StaminaSystem.N.Amount);

        if (this.staminaBar == null)
        {
            return;
        }
        this.staminaBar.SetLineInterval(1f);
        this.staminaBar.SetValues(newStamina, 0f, newMaxStamina);
    }

    internal void compose()
    {
        double width = 850f;
        ElementBounds dialogBounds = new ElementBounds
        {
            Alignment = EnumDialogArea.CenterBottom,
            fixedHeight = 100,
            fixedWidth = width,
        }.WithFixedOffset(0,-22);


        ITreeAttribute staminaTree = this.capi.World.Player.Entity.WatchedAttributes.GetTreeAttribute("stamina");

        var newExausted = staminaTree.GetBool(StaminaSystem.N.Exausted, false);

        var color = newExausted ? ColorUtil.Hex2Doubles("#7f3510") : ColorUtil.Hex2Doubles("#edaa1a");

        ElementBounds staminaStatBar = ElementStdBounds.Statbar(
                EnumDialogArea.CenterMiddle,
                width-2
            ).WithFixedHeight(10);

        this.Composers["valkyr-statbar"] = this.capi.Gui.CreateCompo(
            "valkyr-statbar",
            dialogBounds.FlatCopy().FixedGrow(0.0, 20.0)
            ).BeginChildElements(dialogBounds)
             .AddIf(staminaTree != null)
             .AddStatbar(staminaStatBar, color, "staminastatbar")
             .EndIf()
             .EndChildElements().Compose();

        this.staminaBar = this.Composers["valkyr-statbar"].GetStatbar("staminastatbar");
        this.TryOpen();
    }

    public override bool Focusable => false;
    public override double InputOrder => 2d;

    public override bool ShouldReceiveMouseEvents()
    {
        return false;
    }
}
