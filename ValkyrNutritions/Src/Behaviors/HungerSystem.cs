using System;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace ValkyrNutritions.Behaviors;

internal class HungerSystem : EntityBehavior
{
    public override string PropertyName() => ValkyrNutritionsModSystem.Registry.Get(this.GetType().Name);

    public struct HungerAttrs
    {
        public float maxSaturation;
        public float hungerRateBase;
    }


    private ITreeAttribute hungerTree;
    private float detoxCounter;
    internal long ticker;
    internal HungerAttrs attrs;

    public HungerSystem(Entity entity) : base(entity)
    {
        hungerTree = entity.WatchedAttributes.GetTreeAttribute("hunger");
    }

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        attrs = attributes["props"].AsObject<HungerAttrs>();

        entity.Stats.Set("hungerrate", "base", attrs.hungerRateBase);
        if (hungerTree == null)
        {
            entity.WatchedAttributes.SetAttribute("hunger", hungerTree = new TreeAttribute());
            MaxSaturation = attrs.maxSaturation;
            var satPerNutrient = attrs.maxSaturation / 5f;
            FruitLevel = satPerNutrient;
            VegetableLevel = satPerNutrient;
            ProteinLevel = satPerNutrient;
            GrainLevel = satPerNutrient;
            DairyLevel = satPerNutrient;
        }
        MaxSaturation = attrs.maxSaturation;

        ticker = entity.World.RegisterGameTickListener(new Action<float>(SlowTick), 5000, 0);
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        base.OnEntityDespawn(despawn);
        entity.World.UnregisterGameTickListener(ticker);
    }

    #region Properties

    public float MaxSaturation
    {
        get
        {
            return hungerTree.GetFloat("maxsaturation", 0f);
        }
        set
        {
            hungerTree.SetFloat("maxsaturation", value);
            entity.WatchedAttributes.MarkPathDirty("hunger");
        }
    }

    public float FruitLevel
    {
        get
        {
            return hungerTree.GetFloat("fruitLevel", 0f);
        }
        set
        {
            hungerTree.SetFloat("fruitLevel", value);
            entity.WatchedAttributes.MarkPathDirty("hunger");
        }
    }

    public float VegetableLevel
    {
        get
        {
            return hungerTree.GetFloat("vegetableLevel", 0f);
        }
        set
        {
            hungerTree.SetFloat("vegetableLevel", value);
            entity.WatchedAttributes.MarkPathDirty("hunger");
        }
    }

    public float ProteinLevel
    {
        get
        {
            return hungerTree.GetFloat("proteinLevel", 0f);
        }
        set
        {
            hungerTree.SetFloat("proteinLevel", value);
            entity.WatchedAttributes.MarkPathDirty("hunger");
        }
    }

    public float GrainLevel
    {
        get
        {
            return hungerTree.GetFloat("grainLevel", 0f);
        }
        set
        {
            hungerTree.SetFloat("grainLevel", value);
            entity.WatchedAttributes.MarkPathDirty("hunger");
        }
    }

    public float DairyLevel
    {
        get
        {
            return hungerTree.GetFloat("dairyLevel", 0f);
        }
        set
        {
            hungerTree.SetFloat("dairyLevel", value);
            entity.WatchedAttributes.MarkPathDirty("hunger");
        }
    }

    public float Saturation
    {
        get => FruitLevel + ProteinLevel + GrainLevel + DairyLevel + VegetableLevel;
    }

    private void UpdateSaturation()
    {
        hungerTree.SetFloat("currentsaturation", Saturation);
        entity.WatchedAttributes.MarkPathDirty("hunger");
    }

    #endregion Properties

    public override void OnEntityReceiveSaturation(float saturation, EnumFoodCategory foodCat = EnumFoodCategory.Unknown, float saturationLossDelay = 10f, float nutritionGainMultiplier = 1f)
    {
        float headroom = MaxSaturation - Saturation;
        var fittingSat = Math.Min(headroom, saturation);
        if (headroom > 0f)
        {
            switch (foodCat)
            {
                case EnumFoodCategory.Fruit:
                    FruitLevel = FruitLevel + fittingSat;
                    break;
                case EnumFoodCategory.Vegetable:
                    VegetableLevel = VegetableLevel + fittingSat;
                    break;
                case EnumFoodCategory.Protein:
                    ProteinLevel = ProteinLevel + fittingSat;
                    break;
                case EnumFoodCategory.Grain:
                    GrainLevel = GrainLevel + fittingSat;
                    break;
                case EnumFoodCategory.Dairy:
                    DairyLevel = DairyLevel + fittingSat;
                    break;
            }

            UpdateSaturation();
        }
    }

    public override void OnGameTick(float dt)
    {
        if (entity is EntityPlayer player)
        {
            if (entity.World.PlayerByUid(player.PlayerUID).WorldData.CurrentGameMode != EnumGameMode.Survival)
            {
                return;
            }
        }
        float timeMult = dt * (entity.Api.World.Calendar.SpeedOfTime * entity.Api.World.Calendar.CalendarSpeedMul);
        float saturationUsed = entity.Stats.GetBlended("hungerrate") * (GlobalConstants.HungerSpeedModifier / 30f);

        ReduceSaturation(saturationUsed * timeMult);
        detox(dt);
    }

    private void detox(float dt)
    {
        detoxCounter += dt;
        if (detoxCounter > 1f)
        {
            float intox = entity.WatchedAttributes.GetFloat("intoxication", 0f);
            if (intox > 0f)
            {
                entity.WatchedAttributes.SetFloat("intoxication", Math.Max(0f, intox - 0.005f));
            }
            detoxCounter = 0f;
        }
    }

    public void ReduceSaturation(float saturationUse)
    {
        var sat = Saturation;
        if (sat <= 0f)
        {
            return;
        }
        FruitLevel = Math.Max(0f, FruitLevel - (saturationUse * (FruitLevel / sat)));
        VegetableLevel = Math.Max(0f, VegetableLevel - (saturationUse * (VegetableLevel / sat)));
        ProteinLevel = Math.Max(0f, ProteinLevel - (saturationUse * (ProteinLevel / sat)));
        GrainLevel = Math.Max(0f, GrainLevel - (saturationUse * (GrainLevel / sat)));
        DairyLevel = Math.Max(0f, DairyLevel - (saturationUse * (DairyLevel / sat)));
        UpdateSaturation();
    }

    private void SlowTick(float dt)
    {
        if (entity is EntityPlayer)
        {
            EntityPlayer plr = (EntityPlayer)entity;
            if (entity.World.PlayerByUid(plr.PlayerUID).WorldData.CurrentGameMode == EnumGameMode.Creative)
            {
                return;
            }
        }
        bool harshWinters = entity.World.Config.GetString("harshWinters", null).ToBool(true);
        float temperature = entity.World.BlockAccessor.GetClimateAt(entity.Pos.AsBlockPos, EnumGetClimateMode.ForSuppliedDate_TemperatureOnly, entity.World.Calendar.TotalDays).Temperature;
        if (temperature >= 2f || !harshWinters)
        {
            entity.Stats.Set("hungerrate", "resistcold", 0f);
        }
        else
        {
            float diff = GameMath.Clamp(2f - temperature, 0f, 10f);
            Room room = entity.World.Api.ModLoader.GetModSystem<RoomRegistry>(true).GetRoomForPosition(entity.Pos.AsBlockPos);
            entity.Stats.Set("hungerrate", "resistcold", (room.ExitCount == 0) ? 0f : (diff / 40f), true);
        }
    }

    public override void OnEntityReceiveDamage(DamageSource damageSource, ref float damage)
    {
        if (damageSource.Type == EnumDamageType.Heal && damageSource.Source == EnumDamageSource.Revive)
        {
            VegetableLevel /= 2f;
            ProteinLevel /= 2f;
            FruitLevel /= 2f;
            DairyLevel /= 2f;
            GrainLevel /= 2f;
            UpdateSaturation();
        }
    }
}
