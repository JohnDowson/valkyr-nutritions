using HarmonyLib;
using System.Collections.Generic;
using ValkyrNutritions.Behaviors;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace ValkyrNutritions;

[HarmonyPatch]
public class ValkyrNutritionsModSystem : ModSystem
{
    internal ValkyrHud valkyrHud;
    internal ICoreAPI api;
    internal ICoreServerAPI sapi;
    internal Harmony harmony;

    static internal Dictionary<string, string> Registry = new();
    static internal ILogger Logger = null;

    internal void Register(System.Type type) {
        var className = $"Valkyr:{type.Name}";

        ValkyrNutritionsModSystem.Registry.TryAdd(type.Name, className);
        if (typeof(EntityBehavior).IsAssignableFrom(type))
        {
            api.RegisterEntityBehaviorClass(className, type);
        }

        if (typeof(CollectibleBehavior).IsAssignableFrom(type))
        {
            api.RegisterCollectibleBehaviorClass(className, type);
        }
    }

    // Called on server and client
    // Useful for registering block/entity classes on both sides
    public override void Start(ICoreAPI api)
    {
        ValkyrNutritionsModSystem.Logger = Mod.Logger;

        if (!Harmony.HasAnyPatches(Mod.Info.ModID))
        {
            harmony = new Harmony(Mod.Info.ModID);
            harmony.PatchAll(); // Applies all harmony patches
        }

        this.api = api;
        Register(typeof(StaminaSystem));
        Register(typeof(ToolUseNotifier));
        Register(typeof(NutritionBonus));
        Register(typeof(HungerSystem));
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        sapi = api;
        api.ChatCommands.Create("valkyrreset")
            .RequiresPrivilege("worldedit")
            .WithDescription("Reset health and nutrition attributes")
            .HandleWith(ResetTrees);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {        
        valkyrHud = new ValkyrHud(api);
    }

    public TextCommandResult ResetTrees(TextCommandCallingArgs args)
    {
        foreach (var player in api.World.AllPlayers)
        {
            foreach (var bhj in player.Entity.SidedProperties.BehaviorsAsJsonObj)
            {
                var ent = player.Entity;
                var code = bhj["code"]?.AsString(null);
                if (code == "health")
                {

                    ent.WatchedAttributes.RemoveAttribute("health");
                    var hb = ent.GetBehavior<EntityBehaviorHealth>();
                    hb.Initialize(ent.Properties, bhj);
                }

            }
        }

        return TextCommandResult.Success();
    }

    #region Patches

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemBow), nameof(ItemBow.OnHeldInteractStop))]
    public static bool BowInteractStopPatch(ItemBow __instance, float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel)
    {
        bool preventDefault = false;
        foreach (CollectibleBehavior collectibleBehavior in __instance.CollectibleBehaviors)
        {
            EnumHandling handled = EnumHandling.PassThrough;
            collectibleBehavior.OnHeldInteractStop(secondsUsed, slot, byEntity, blockSel, entitySel, ref handled);
            if (handled != EnumHandling.PassThrough)
            {
                preventDefault = true;
            }
            if (handled == EnumHandling.PreventSubsequent)
            {
                return true;
            }
        }
        return !preventDefault;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ItemBow), nameof(ItemBow.OnHeldInteractCancel))]
    public static bool BowInteractCancelPatch(ItemBow __instance, float secondsUsed, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, EnumItemUseCancelReason cancelReason)
    {
        bool result = true;
        bool preventDefault = false;
        foreach (CollectibleBehavior collectibleBehavior in __instance.CollectibleBehaviors)
        {
            EnumHandling handled = EnumHandling.PassThrough;
            bool behaviorResult = collectibleBehavior.OnHeldInteractCancel(secondsUsed, slot, byEntity, blockSel, entitySel, cancelReason, ref handled);
            if (handled != EnumHandling.PassThrough)
            {
                result = (result && behaviorResult);
                preventDefault = true;
            }
            if (handled == EnumHandling.PreventSubsequent)
            {
                return result;
            }
        }


        return !preventDefault;
    }
    #endregion
}
