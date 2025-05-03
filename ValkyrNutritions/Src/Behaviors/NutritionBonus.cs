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
    }

    public struct NutritionAttrs
    {
        public Nutrients staminaAmountFrom;
        public Nutrients staminaRecoveryRateFrom;
    }

    public override string PropertyName()
    {
        return ValkyrNutritionsModSystem.Registry.Get(this.GetType().Name);
    }
    internal EntityBehaviorHunger hungerBehavior;
    internal StaminaSystem staminaBehavior;
    internal long ticker;
    internal NutritionAttrs attrs;

    public NutritionBonus(Entity entity) : base(entity)
    {
    }

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        attrs = attributes["props"].AsObject<NutritionAttrs>();
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        hungerBehavior = entity.GetBehavior<EntityBehaviorHunger>();

        ticker = entity.World.RegisterGameTickListener(new Action<float>(SlowTick), 5000, 0);
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        base.OnEntityDespawn(despawn);
        entity.World.UnregisterGameTickListener(ticker);
    }

    public void SlowTick(float deltaTime)
    {
        if (hungerBehavior == null) return;

        var maxSat = hungerBehavior.MaxSaturation;
        
        var pprotein = hungerBehavior.ProteinLevel / maxSat;
        var pfruit = hungerBehavior.FruitLevel / maxSat;
        var pgrain = hungerBehavior.GrainLevel / maxSat;
        var pdairy = hungerBehavior.DairyLevel / maxSat;
        var pvegetable = hungerBehavior.VegetableLevel / maxSat;

        //var eprotein = ((pprotein * 0.8f) + (pdairy * 0.2f));
        //var ecarbs = ((pgrain * 0.7f) + (pfruit * 0.3f));
        //var egreens = ((pvegetable * 0.6f) + (pfruit * 0.4f));

        //var overcarb = (ecarbs + 1) - eprotein;

        //var staminaRegenT = (egreens + (ecarbs * overcarb)) / 2f;

        //var staminaAmountT = (egreens + eprotein) / 2f;

        var staminaAmount = pprotein * attrs.staminaAmountFrom.protein;
        staminaAmount += pfruit * attrs.staminaAmountFrom.fruit;
        staminaAmount += pgrain * attrs.staminaAmountFrom.grain;
        staminaAmount += pdairy * attrs.staminaAmountFrom.dairy;
        staminaAmount += pvegetable * attrs.staminaAmountFrom.vegetable;
        entity.Stats.Set(StaminaSystem.N.Amount, "nutrition", staminaAmount);

        var staminaRegen = pprotein * attrs.staminaAmountFrom.protein;
        staminaRegen += pfruit * attrs.staminaAmountFrom.fruit;
        staminaRegen += pgrain * attrs.staminaAmountFrom.grain;
        staminaRegen += pdairy * attrs.staminaAmountFrom.dairy;
        staminaRegen += pvegetable * attrs.staminaAmountFrom.vegetable;
        entity.Stats.Set(StaminaSystem.N.Regen, "nutrition", staminaRegen);
    }
}
