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
        public float recoveryRateSitting;
    }

    internal readonly struct N {
        internal static readonly string Attr = "stamina";
        internal static readonly string Current = "current";

        internal static readonly string Amount = "staminaAmount";
        internal static readonly string Regen = "staminaRegen";
        internal static readonly string Exausted = "exausted";
    };

    private ILogger logger;

    internal StaminaAttrs attrs;
    private bool exausted = false;
    private bool groundedThisTick;
    private long spentStamina;
    private EntityPlayer player;
    private ITreeAttribute staminaTree;

    #region Properties

    internal float Current {
        get {
            return staminaTree.GetFloat(N.Current);
        }
        set {
            staminaTree.SetFloat(N.Current,
                GameMath.Clamp(value, 0f, player.Stats.GetBlended(N.Amount)));
            player.WatchedAttributes.MarkPathDirty(N.Attr);
        }
    }

    public bool Exausted
    {
        get { return exausted; }
        set
        {
            if (value)
            {
                player.Stats.Set("walkspeed", "lowstamina", -0.3f, true);
                player.Stats.Set("jumpHeightMul", "lowstamina", 0f, true);
                player.Stats.Set("hungerrate", "lowstamina", 1f, true);
            }
            else
            {
                player.Stats.Set("walkspeed", "lowstamina", 0f, true);
                player.Stats.Set("jumpHeightMul", "lowstamina", 0.33f, true);
                player.Stats.Set("hungerrate", "lowstamina", 0f, true);
            }
            exausted = value;
            staminaTree.SetBool("exausted", value);
            entity.WatchedAttributes.MarkPathDirty(N.Attr);
        }
    }

    #endregion

    public StaminaSystem(Entity entity) : base(entity)
    {
        player = (EntityPlayer)entity;
        staminaTree = this.entity.WatchedAttributes.GetTreeAttribute(N.Attr);
    }

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        attrs = attributes["props"].AsObject<StaminaAttrs>();

        player.Stats.Set(N.Regen, "base", attrs.recoveryRateBase, true);
        player.Stats.Set(N.Amount, "base", attrs.amountMaxBase, true);

        if (staminaTree == null)
        {
            entity.WatchedAttributes.SetAttribute("stamina", staminaTree = new TreeAttribute());

            Current = attrs.amountMaxBase;
        }

        exausted = staminaTree.GetBool("exausted");
    }

    public override void OnGameTick(float dt)
    {
        if (staminaTree == null)
        {
            return;
        }

        if (groundedThisTick = Grounded(dt))
        {
            if (player.ServerControls.Sprint && player.ServerControls.TriesToMove)
            {
                SpendStamina(2f * dt);
            }
            if (player.ServerControls.Jump && entity.World.ElapsedMilliseconds - lastJump > 500L && entity.Alive)
            {
                lastJump = player.World.ElapsedMilliseconds;
                SpendStamina(150f * dt);
            }
        }

        if (!exausted && Current <= 0.01f)
        {
            Exausted = true;
        }

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
        spentStamina = player.World.ElapsedMilliseconds;
    }

    private void Recover(float dt)
    {
        var elapsed = player.World.ElapsedMilliseconds - spentStamina;
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

            var rate = player.Stats.GetBlended(N.Regen);
            rate += groundedThisTick && player.Controls.FloorSitting ?
                    attrs.recoveryRateSitting : 0f;
            rate *= idleBonus;

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

