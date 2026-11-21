using Content.Server.Light.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Robust.Server.GameObjects;

namespace Content.Server._Floof.Light;

public sealed partial class BackupLightSystem : EntitySystem
{
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private PointLightSystem _pointLight = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<BackupLightComponent> entity, ref MapInitEvent args)
    {
        UpdateState(entity);
    }

    [SubscribeLocalEvent]
    private void OnBackupPower(Entity<BackupLightComponent> entity, ref PowerChangedEvent args)
    {
        var meta = MetaData(entity.Owner);

        // TODO: PowerChangedEvent shouldn't be issued for paused ents but this is the world we live in.
        if (meta.EntityLifeStage >= EntityLifeStage.Terminating ||
            meta.EntityPaused)
        {
            return;
        }

        UpdateState(entity);
    }

    public void SetState(EntityUid uid, BackupLightComponent component, BackupLightState state)
    {
        if (component.State == state) return;

        component.State = state;

        switch (state)
        {
            case BackupLightState.On:
            case BackupLightState.Charging:
                EnsureComp<ActiveEmergencyLightComponent>(uid);
                break;
            case BackupLightState.Full:
            case BackupLightState.Empty:
                RemComp<ActiveEmergencyLightComponent>(uid);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ActiveEmergencyLightComponent, BackupLightComponent, BatteryComponent>();
        while (query.MoveNext(out var uid, out _, out var emergencyLight, out var battery))
        {
            Update((uid, emergencyLight), battery, frameTime);
        }
    }

    private void Update(Entity<BackupLightComponent> entity, BatteryComponent battery, float frameTime)
    {
        if (entity.Comp.State == BackupLightState.On)
        {
            if (!_battery.TryUseCharge((entity.Owner, battery), entity.Comp.Wattage * frameTime))
            {
                SetState(entity.Owner, entity.Comp, BackupLightState.Empty);
                TurnOff(entity);
            }
        }
        else
        {
            _battery.ChangeCharge((entity.Owner, battery), entity.Comp.ChargingWattage * frameTime * entity.Comp.ChargingEfficiency);
            if (_battery.IsFull((entity.Owner, battery)))
            {
                if (TryComp<ApcPowerReceiverComponent>(entity.Owner, out var receiver))
                {
                    receiver.Load = 1;
                }

                SetState(entity.Owner, entity.Comp, BackupLightState.Full);
            }
        }
    }

    /// <summary>
    ///     Updates the light's power drain, battery drain, sprite and actual light state.
    /// </summary>
    public void UpdateState(Entity<BackupLightComponent> entity)
    {
        if (!TryComp<ApcPowerReceiverComponent>(entity.Owner, out var receiver))
            return;

        if (receiver.Powered)
        {
            receiver.Load = (int)Math.Abs(entity.Comp.Wattage);
            TurnOff(entity);
            SetState(entity.Owner, entity.Comp, BackupLightState.Charging);
        }
        else // If internal battery runs out it will end in off red state
        {
            TurnOn(entity);
            SetState(entity.Owner, entity.Comp, BackupLightState.On);
        }
    }

    private void TurnOff(Entity<BackupLightComponent> entity)
    {
        _pointLight.SetEnabled(entity.Owner, false);
        _appearance.SetData(entity.Owner, EmergencyLightVisuals.On, false);
    }

    private void TurnOn(Entity<BackupLightComponent> entity)
    {
        _pointLight.SetEnabled(entity.Owner, true);
        _appearance.SetData(entity.Owner, EmergencyLightVisuals.On, true);
    }
}
