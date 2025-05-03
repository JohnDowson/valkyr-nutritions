using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace ValkyrNutritions.Behaviors;

public class ToolUseNotifier : CollectibleBehavior
{
    public float staminaCostMomentarty;
    public float? staminaCostHeld;

    public ToolUseNotifier(CollectibleObject collObj) : base(collObj)
    {
    }

    public override void Initialize(JsonObject properties)
    {
        base.Initialize(properties);
        staminaCostMomentarty = properties["staminaCostMomentarty"].AsFloat(1f);
        try
        {
            staminaCostHeld = properties["staminaCostHeld"].AsObject<float?>(null);
        }
        catch (Exception)
        {
            staminaCostHeld = null;
        }
    }

    public override bool OnBlockBrokenWith(IWorldAccessor world, Entity entity, ItemSlot itemslot, BlockSelection blockSel, float dropQuantityMultiplier, ref EnumHandling handling)
    {
        return notifyStaminaSystem(entity, ref handling);
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent entity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling)
    {
        notifyStaminaSystem(entity, ref handling);
    }

    public override bool OnHeldInteractCancel(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason, ref EnumHandling handled)
    {
        if (byEntity is EntityPlayer player)
        {
            StaminaSystem stamina;
            if ((stamina = player.GetBehavior<StaminaSystem>()) != null)
                stamina.RemoveTicker();
        }

        return base.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason, ref handled);
    }

    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, ref EnumHandling handling)
    {
        base.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handling);
        if (byEntity is not EntityPlayer player)
            return;

        StaminaSystem stamina;
        if ((stamina = player.GetBehavior<StaminaSystem>()) == null)
            return;

        stamina.RemoveTicker();
    }

    internal bool notifyStaminaSystem(Entity holder, ref EnumHandling handling) 
    {
        handling = EnumHandling.PassThrough;

        if (holder is not EntityPlayer player)
            return true;

        StaminaSystem stamina;
        if ((stamina = player.GetBehavior<StaminaSystem>()) == null)
            return true;

        if (stamina.TrySpendStamina(staminaCostMomentarty))
        {
            if (staminaCostHeld.HasValue)
                stamina.SetTicker(staminaCostHeld.Value);

            return true;
        }

        handling = EnumHandling.PreventSubsequent;
        return false;
    }
}
