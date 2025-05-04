using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace ValkyrNutritions.Behaviors;

public class NutritionBonus : EntityBehavior
{
    public struct NutritionAttrs
    {
        public float healthMaxBonus;
        public float healthRegenMaxBonus;
        public float staminaMaxBonus;
        public float staminaRegenMaxBonus;
        public float moveSpeedMaxBonus;
    }


    internal ITreeAttribute hungerTree;
    internal StaminaSystem staminaBehavior;
    internal long ticker;
    internal NutritionAttrs attrs;

    public override string PropertyName() => ValkyrNutritionsModSystem.Registry.Get(this.GetType().Name);
    public NutritionBonus(Entity entity) : base(entity)
    {
    }

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        attrs = attributes["props"].AsObject<NutritionAttrs>();
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        hungerTree = entity.WatchedAttributes.GetTreeAttribute("hunger");
        ticker = entity.World.RegisterGameTickListener(new Action<float>(SlowTick), 5000, 0);
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        base.OnEntityDespawn(despawn);
        entity.World.UnregisterGameTickListener(ticker);
    }

    public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
    {
        base.OnEntityReceiveDamage(damageSource, ref damage);
    }

    #region Properties
    #endregion Properties

    public void SlowTick(float deltaTime)
    {
        if (hungerTree == null) return;

        var maxSat = hungerTree.GetFloat("maxsaturation");
        var starving = hungerTree.GetFloat("currentsaturation") <= 0f;

        var pprotein = hungerTree.GetFloat("proteinLevel") / maxSat;
        var pfruit = hungerTree.GetFloat("fruitLevel") / maxSat;
        var pgrain = hungerTree.GetFloat("grainLevel") / maxSat;
        var pdairy = hungerTree.GetFloat("dairyLevel") / maxSat;
        var pvegetable = hungerTree.GetFloat("vegetableLevel") / maxSat;

        var eproitein = pprotein + pdairy;
        var ecarb = pgrain + pfruit;
        var evitamin = pvegetable + pfruit;

        var staminaAmount = attrs.staminaMaxBonus * eproitein;
        entity.Stats.Set(StaminaSystem.N.Amount, "nutrition", staminaAmount);

        var staminaRegen = attrs.staminaRegenMaxBonus * ecarb;
        entity.Stats.Set(StaminaSystem.N.Regen, "nutrition", staminaRegen);

        var moveSpeed = attrs.moveSpeedMaxBonus * eproitein;
        entity.Stats.Set("walkspeed", "nutritrition", moveSpeed, true);

        //var exertionCost = attrs.__ * eproitein;
        //entity.Stats.Set(StaminaSystem.N.JumpDrain, "nutrition", exertionCost);
        //entity.Stats.Set(StaminaSystem.N.SprintDrain, "nutrition", exertionCost);

        EntityBehaviorHealth hb = entity.GetBehavior<EntityBehaviorHealth>();
        var healthBonus = attrs.healthMaxBonus * evitamin;
        hb.SetMaxHealthModifiers("nutrition", healthBonus);
    }
}
