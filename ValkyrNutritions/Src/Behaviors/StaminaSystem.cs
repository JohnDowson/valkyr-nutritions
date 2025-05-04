using Newtonsoft.Json.Linq;
using System;
using Vintagestory;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace ValkyrNutritions.Behaviors;

public class StaminaSystem : EntityBehavior
{
    public struct StaminaAttrs
    {
        public float amountMaxBase;
        public float recoveryRateBase;
        public float recoveryRateSittingMult;
        public float sprintDrainBase;
        public float jumpDrainBase;
    }

    internal readonly struct N
    {
        internal static readonly string Attr = "stamina";
        internal static readonly string Current = "current";

        internal static readonly string Amount = "staminaAmount";
        internal static readonly string Regen = "staminaRegen";
        internal static readonly string Exausted = "exausted";
        internal static readonly string SprintDrain = "sprintDrain";
        internal static readonly string JumpDrain = "jumpDrain";
    };

    private ILogger logger;

    internal StaminaAttrs attrs;
    private EntityAgent agent;
    private bool groundedThisTick;
    private long spentStamina;
    private ITreeAttribute staminaTree;

    #region Properties

    internal float Current
    {
        get
        {
            return staminaTree.GetFloat(N.Current);
        }
        set
        {
            staminaTree.SetFloat(N.Current,
                GameMath.Clamp(value, 0f, agent.Stats.GetBlended(N.Amount)));
            agent.WatchedAttributes.MarkPathDirty(N.Attr);
        }
    }

    public bool Exausted
    {
        get { return staminaTree.GetBool("exausted", false); }
        set
        {
            if (value)
            {
                agent.Stats.Set("walkspeed", "lowstamina", -0.3f, true);
                agent.Stats.Set("jumpHeightMul", "lowstamina", 0f, true);
                agent.Stats.Set("hungerrate", "lowstamina", 1f, true);
            }
            else
            {
                agent.Stats.Set("walkspeed", "lowstamina", 0f, true);
                agent.Stats.Set("jumpHeightMul", "lowstamina", 0.33f, true);
                agent.Stats.Set("hungerrate", "lowstamina", 0f, true);
            }
            staminaTree.SetBool(N.Exausted, value);
            entity.WatchedAttributes.MarkPathDirty(N.Attr);
        }
    }

    #endregion

    public StaminaSystem(Entity entity) : base(entity)
    {
        agent = (EntityAgent)entity;
        staminaTree = this.entity.WatchedAttributes.GetTreeAttribute(N.Attr);
    }

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        attrs = attributes["props"].AsObject<StaminaAttrs>();

        agent.Stats.Set(N.Regen, "base", attrs.recoveryRateBase, true);
        agent.Stats.Set(N.Amount, "base", attrs.amountMaxBase, true);

        agent.Stats.Set(N.SprintDrain, "base", attrs.sprintDrainBase, true);
        agent.Stats.Set(N.JumpDrain, "base", attrs.jumpDrainBase, true);

        if (staminaTree == null)
        {
            entity.WatchedAttributes.SetAttribute("stamina", staminaTree = new TreeAttribute());

            Current = attrs.amountMaxBase;
        }
    }

    public override void OnGameTick(float dt)
    {
        if (staminaTree == null || agent == null)
        {
            return;
        }

        Current = Math.Min(Current, agent.Stats.GetBlended(N.Amount));

        if (groundedThisTick = Grounded(dt))
        {
            if (agent.ServerControls.Sprint && agent.ServerControls.TriesToMove)
            {
                SpendStamina(agent.Stats.GetBlended(N.SprintDrain) * dt);
            }
            if (agent.ServerControls.Jump && entity.World.ElapsedMilliseconds - lastJump > 500L && entity.Alive)
            {
                lastJump = agent.World.ElapsedMilliseconds;
                SpendStamina(agent.Stats.GetBlended(N.JumpDrain));
            }
        }

        if (!Exausted && Current <= 0.01f)
        {
            Exausted = true;
        }

        if (Current < agent.Stats.GetBlended(N.Amount))
            Recover(dt);
    }

    public bool TrySpendStamina(float use)
    {
        if (use >= Current)
        {
            return false;
        }

        SpendStamina(use);
        return true;
    }

    private void SpendStamina(float use)
    {
        Current -= use;
        spentStamina = agent.World.ElapsedMilliseconds;
    }

    private void Recover(float dt)
    {
        var elapsed = agent.World.ElapsedMilliseconds - spentStamina;
        if (elapsed > 1000L)
        {
            const float SecondsToMaxRecSpd = 10f;
            const float MaxIdleRecBonus = 2f;
            var elapsedF = GameMath.Clamp(
                ((float)elapsed) / (SecondsToMaxRecSpd * 1000f),
                0f,
                1f
            );

            var idleBonus = GameMath.Lerp(0f, MaxIdleRecBonus, elapsedF);

            var rate = agent.Stats.GetBlended(N.Regen);
            rate *= idleBonus;

            var hs = entity.GetBehavior<HungerSystem>();
            hs?.ReduceSaturation((rate * dt) * 2f);

            if (groundedThisTick && agent.Controls.FloorSitting)
                rate *= attrs.recoveryRateSittingMult;

            Current += rate * dt;
        }
        if (Exausted && Current > 10f)
        {
            Exausted = false;
        }
    }

    public override string PropertyName()
    {
        return ValkyrNutritionsModSystem.Registry.Get(this.GetType().Name);
    }

    private bool Grounded(float dt)
    {
        bool flag = entity.OnGround && !entity.Swimming;
        if (flag && antiCoyoteTimer <= 0f)
        {
            coyoteTimer = 0.15f;
        }
        if (coyoteTimer > 0f && entity.Attributes.GetInt("dmgkb", 0) > 0)
        {
            coyoteTimer = 0f;
            antiCoyoteTimer = 0.16f;
        }

        if (flag || coyoteTimer > 0f)
        {
            coyoteTimer -= dt;
            antiCoyoteTimer = Math.Max(0f, antiCoyoteTimer - dt);
            return true;
        }
        return false;
    }

    private long lastJump;
    private float coyoteTimer;
    private float antiCoyoteTimer;

    public void SetTicker(float use)
    {
        if (_ticker.HasValue)
        {
            this.entity.Api.Event.UnregisterGameTickListener(_ticker.Value);
        }
        _ticker = this.entity.Api.Event.RegisterGameTickListener(
            new Action<float>(
                (float dt) => { this.TrySpendStamina(use * dt); }
            ),
            20, 100
        );
    }

    public void RemoveTicker()
    {
        if (_ticker.HasValue)
        {
            this.entity.Api.Event.UnregisterGameTickListener(_ticker.Value);
            _ticker = null;
        }
    }

    private long? _ticker = null;
}

