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
    public struct Nutrients
    {
        public float protein;
        public float fruit;
        public float grain;
        public float dairy;
        public float vegetable;

        public float Calculate(float pprotein, float pfruit, float pgrain, float pdairy, float pvegetable)
        {
            var effect = pprotein * protein;
            effect += pfruit * fruit;
            effect += pgrain * grain;
            effect += pdairy * dairy;
            effect += pvegetable * vegetable;
            return effect;
        }
    }

    public struct NutritionAttrs
    {
        public Nutrients staminaAmountFrom;
        public Nutrients staminaRecoveryRateFrom;
        public Nutrients exertionStaminaCostFrom;
        public Nutrients healthBonusFrom;
        public float healthBonusAmount;
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

        var staminaAmount = attrs.staminaAmountFrom.Calculate(pprotein, pfruit, pgrain, pdairy, pvegetable);
        entity.Stats.Set(StaminaSystem.N.Amount, "nutrition", staminaAmount);

        var staminaRegen = attrs.staminaAmountFrom.Calculate(pprotein, pfruit, pgrain, pdairy, pvegetable);
        entity.Stats.Set(StaminaSystem.N.Regen, "nutrition", staminaRegen);

        var exertionCost = attrs.exertionStaminaCostFrom.Calculate(pprotein, pfruit, pgrain, pdairy, pvegetable);
        entity.Stats.Set(StaminaSystem.N.JumpDrain, "nutrition", exertionCost);
        entity.Stats.Set(StaminaSystem.N.SprintDrain, "nutrition", exertionCost);

        var healthBonus = attrs.healthBonusFrom.Calculate(pprotein, pfruit, pgrain, pdairy, pvegetable) * attrs.healthBonusAmount;
        EntityBehaviorHealth hb = entity.GetBehavior<EntityBehaviorHealth>();
        hb.SetMaxHealthModifiers("nutrition", healthBonus);
    }
}
