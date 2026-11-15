using Content.Shared.Humanoid;
using Content.Shared.Alert;
using Robust.Server.GameObjects;
using Content.Shared.Examine;
using Robust.Server.Containers;
using Content.Shared._Starlight;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared._Goobstation.Overlays; // Delta V - Flash Work
using Robust.Shared.Timing;
using System.Linq;
using Content.Server.Actions;
using Content.Shared._Goobstation.Flashbang;
using Content.Shared._Starlight.Flash.Components;
using Content.Shared._Starlight.Humanoid;
using Content.Shared.Body.Components;
using Content.Shared.Flash;
using Content.Shared.Overlays;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight;

public sealed partial class ShadekinSystem : EntitySystem
{
    [Dependency] private ActionsSystem _actions = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private ContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedFlashSystem _flashSystem = default!;

    private sealed class LightCone
    {
        public float Direction { get; set; }
        public float InnerWidth { get; set; }
        public float OuterWidth { get; set; }
    }
    private readonly Dictionary<string, List<LightCone>> _lightMasks = new()
    {
            ["ConeSingle"] = new List<LightCone>
        {
            new() { Direction = 0, InnerWidth = 30, OuterWidth = 60 }
        },
            ["ConeDouble"] = new List<LightCone>
        {
            new() { Direction = 0, InnerWidth = 30, OuterWidth = 60 },
            new() { Direction = 180, InnerWidth = 30, OuterWidth = 60 }
        }
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShadekinComponent, ComponentStartup>(OnInit);
        SubscribeLocalEvent<ShadekinComponent, MapInitEvent>(OnMapInit); // Floof: Added.
        SubscribeLocalEvent<ShadekinComponent, ToggleNightVisionEvent>(OnToggleNightVision); // Floof: Added.
        SubscribeLocalEvent<ShadekinComponent, ComponentShutdown>(OnShutdown); // Floof: Added.
        SubscribeLocalEvent<ShadekinComponent, EyeColorInitEvent>(OnEyeColorChange);
        SubscribeLocalEvent<ShadekinComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeedModifiers);
        SubscribeLocalEvent<ShadekinComponent, AfterFlashedEvent>(OnShadekinFlashed); // Delta V - Prevent Chain Flashing
        SubscribeLocalEvent<ShadekinComponent, FlashDurationMultiplierEvent>(GetFlashModifier); // Delta V - Flash Modifier to Shadekin
    }

    private void OnShutdown(Entity<ShadekinComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ToggleNightVisionActionEntity);
    }

    private void OnMapInit(Entity<ShadekinComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ToggleNightVisionActionEntity, _actionToggleNightVision);
    }

    private readonly EntProtoId _actionToggleNightVision = "ActionToggleNightVision";

    private void OnToggleNightVision(Entity<ShadekinComponent> ent, ref ToggleNightVisionEvent args)
    {
        if (!TryComp<NightVisionComponent>(ent.Owner, out var nightVision))
            return;

        if (args.Handled)
            return;

        if (!nightVision.Enabled)
            return;

        var state = ent.Comp.CurrentState;
        _flashSystem.Flash(ent.Owner, ent.Owner, ent.Owner, TimeSpan.FromSeconds(0.5 * (int)state), 0.5f);
    }

    private void OnInit(EntityUid uid, ShadekinComponent component, ComponentStartup args)
    {
        UpdateAlert(uid, component, (short)component.CurrentState);
        RemComp<InternalsComponent>(uid);
    }

    private void OnEyeColorChange(EntityUid uid, ShadekinComponent component, EyeColorInitEvent args)
    {
        if (!TryComp<HumanoidProfileComponent>(uid, out var humanoid))
            return;

        // humanoid.EyeColor = Color.Black;
        Dirty(uid, humanoid);
    }

    public void UpdateAlert(EntityUid uid, ShadekinComponent component, short state)
    {
        _alerts.ShowAlert(uid, component.ShadekinAlert, state);
    }

    private Angle GetAngle(EntityUid lightUid, SharedPointLightComponent lightComp, EntityUid targetUid)
    {
        var (lightPos, lightRot) = _transform.GetWorldPositionRotation(lightUid);
        lightPos += lightRot.RotateVec(lightComp.Offset);

        var (targetPos, targetRot) = _transform.GetWorldPositionRotation(targetUid);

        var mapDiff = targetPos - lightPos;

        var oppositeMapDiff = (-lightRot).RotateVec(mapDiff);
        var angle = oppositeMapDiff.ToWorldAngle();

        if (angle == double.NaN && _transform.ContainsEntity(targetUid, lightUid) || _transform.ContainsEntity(lightUid, targetUid))
        {
            angle = 0f;
        }

        return angle;
    }

    /// <summary>
    /// Return an illumination float value with is how many "energy" of light is hitting our ent.
    /// WARNING: This function might be expensive, Avoid calling it too much and CACHE THE RESULT!
    /// </summary>
    /// <param name="uid"></param>
    /// <returns></returns>
    public float GetLightExposure(EntityUid uid)
    {
        var illumination = 0f;

        var lightQuery = _lookup.GetEntitiesInRange<PointLightComponent>(Transform(uid).Coordinates, 20, LookupFlags.Uncontained);

        foreach (var light in lightQuery)
        {
            if (!light.Comp.Enabled
                || light.Comp.Radius < 1
                || light.Comp.Energy <= 0)
                continue;

            var (lightPos, lightRot) = _transform.GetWorldPositionRotation(light);
            lightPos += lightRot.RotateVec(light.Comp.Offset);

            if (!_examine.InRangeUnOccluded(light, uid, light.Comp.Radius, null))
                continue;

            Transform(uid).Coordinates.TryDistance(EntityManager, Transform(light).Coordinates, out var dist);

            var denom = dist / light.Comp.Radius;
            var attenuation = 1 - (denom * denom);
            var calculatedLight = 0f;

            if (light.Comp.LightMask is not null)
            {
                var angleToTarget = GetAngle(light, light.Comp, uid);
                foreach (var cone in _lightMasks[light.Comp.LightMask])
                {
                    var coneLight = 0f;
                    var angleAttenuation = (float)Math.Min((float)Math.Max(cone.OuterWidth - angleToTarget, 0f), cone.InnerWidth) / cone.OuterWidth;

                    if (angleToTarget.Degrees - cone.Direction > cone.OuterWidth)
                        continue;
                    else if (angleToTarget.Degrees - cone.Direction > cone.InnerWidth
                        && angleToTarget.Degrees - cone.Direction < cone.OuterWidth)
                        coneLight = light.Comp.Energy * attenuation * attenuation * angleAttenuation;
                    else
                        coneLight = light.Comp.Energy * attenuation * attenuation;

                    calculatedLight = Math.Max(calculatedLight, coneLight);
                }
            }
            else
                calculatedLight = light.Comp.Energy * attenuation * attenuation;

            illumination += calculatedLight; //Math.Max(illumination, calculatedLight);
        }

        return illumination;
    }

    private void SetPassiveBuff(EntityUid uid, ShadekinState state)
    {
        if (!TryComp<PassiveDamageComponent>(uid, out var passive))
            return;

        if (state is ShadekinState.Extreme or ShadekinState.Annoying or ShadekinState.High)
        {
            // passive.DamageCap = 1;
        }
        else if (state == ShadekinState.Low)
        {
            // passive.DamageCap = 20;
            passive.AllowedStates.Clear();
            passive.AllowedStates.Add(MobState.Alive);
            passive.Interval = TimeSpan.FromSeconds(1);
        }
        else if (state != ShadekinState.Dark)
        {
            // passive.DamageCap = 0;
            passive.AllowedStates.Clear();
            passive.AllowedStates.Add(MobState.Alive);
            passive.AllowedStates.Add(MobState.Critical);
            passive.AllowedStates.Add(MobState.Dead);
            passive.Interval = TimeSpan.FromSeconds(0.5);
        }
    }

    private void ApplyLightDamage(EntityUid uid, float dmg)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict.Add("Heat", dmg);
        _damageable.TryChangeDamage(uid, damage, true, false);

    }

    private void OnRefreshMovementSpeedModifiers(EntityUid uid, ShadekinComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (component.CurrentState == ShadekinState.Low || component.CurrentState == ShadekinState.Annoying ||
                component.CurrentState == ShadekinState.Dark || component.CurrentState == ShadekinState.Invalid)
            return;

        if (!TryComp<MovementSpeedModifierComponent>(uid, out var movement))
            return;

        var sprintDif = movement.BaseWalkSpeed / movement.BaseSprintSpeed;
        args.ModifySpeed(1f, sprintDif);
    }

    private ShadekinState GetStateByThreshold(ShadekinComponent component, float lightExposure)
    {
        var returnState = ShadekinState.Dark;

        foreach (var (threshold, shadekinState) in component.Thresholds.Reverse())
        {
            if (threshold <= lightExposure)
            {
                returnState = shadekinState;
                break;
            }
        }

        return returnState;
    }

    // Delta V - Begin Shadekin Flash Changes
    private void GetFlashModifier(EntityUid uid, ShadekinComponent comp, FlashDurationMultiplierEvent args)
    {
        if (!TryComp<FlashModifierComponent>(uid, out var flashModifier))
            return;

        args.Multiplier = flashModifier.Modifier;
    }

    private void OnShadekinFlashed(EntityUid uid, ShadekinComponent comp, AfterFlashedEvent ev)
    {
        if (!TryComp<NightVisionComponent>(uid, out var nightVision))
            return;

        // This has to be inverted for some reason - no idea why.
        nightVision.Enabled = true;
    }
    // Delta V - End

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ShadekinComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_timing.CurTime < component.NextUpdate)
                continue;

            component.NextUpdate = _timing.CurTime + component.UpdateCooldown;

            var lightExposure = 0f;

            if (!_container.IsEntityInContainer(uid))
                lightExposure = GetLightExposure(uid);

            component.CurrentState = GetStateByThreshold(component, lightExposure);

            UpdateAlert(uid, component, (short)component.CurrentState);

            SetPassiveBuff(uid, component.CurrentState);
            // FLOOF: Swap to normal night vision.
            //ToggleNightVision(uid, component.CurrentState);

            _speed.RefreshMovementSpeedModifiers(uid);

            if (component.CurrentState == ShadekinState.Extreme)
                ApplyLightDamage(uid, 5);
        }
    }
}
