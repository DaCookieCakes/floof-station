using Content.Client._Floof.Movement;
using Content.Client.Silicons.Borgs;
using Content.Client.Toggleable;
using Content.Shared._Floof.Silicons.Borgs;
using Content.Shared.Movement.Components;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client._Floof.Silicons.Borgs;

/// <summary>
/// Picks dogborg body, eye and headlamp states from pose and movement, and keeps their animations in sync.
/// </summary>
/// <remarks>
/// Runs after <see cref="BorgSystem"/> and <see cref="ToggleableVisualsSystem"/> so it has the final say over
/// layer states. Visibility is left to those systems, except while posed, where eyes and headlamp are hidden.
/// </remarks>
public sealed partial class DogborgVisualsSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DogborgComponent, AppearanceChangeEvent>(OnAppearanceChange,
            after: [typeof(BorgSystem), typeof(ToggleableVisualsSystem)]);
        SubscribeLocalEvent<DogborgComponent, SpriteMovementUpdatedEvent>(OnMovementUpdated);
    }

    private void OnAppearanceChange(Entity<DogborgComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite != null)
            UpdateVisuals((ent, args.Sprite));
    }

    private void OnMovementUpdated(Entity<DogborgComponent> ent, ref SpriteMovementUpdatedEvent args)
    {
        if (TryComp(ent, out SpriteComponent? sprite))
            UpdateVisuals((ent, sprite));
    }

    private void UpdateVisuals(Entity<SpriteComponent> ent)
    {
        if (!TryComp(ent, out BorgSwitchableTypeComponent? switchable)
            || !_proto.Resolve(switchable.SelectedBorgType, out var type))
            return;

        var prefix = type.SpriteBodyState;

        _appearance.TryGetData(ent, DogborgVisuals.Pose, out DogborgPose pose);

        var poseState = pose switch
        {
            DogborgPose.Sit => $"{prefix}-sit",
            DogborgPose.Rest => $"{prefix}-rest",
            DogborgPose.BellyUp => $"{prefix}-bellyup",
            _ => null,
        };

        if (poseState != null && HasState(ent, BorgVisualLayers.Body, poseState))
        {
            // No posed eye/headlamp sprites exist, so hide them.
            _sprite.LayerSetRsiState(ent.AsNullable(), BorgVisualLayers.Body, poseState);
            _sprite.LayerSetVisible(ent.AsNullable(), BorgVisualLayers.Light, false);
            _sprite.LayerSetVisible(ent.AsNullable(), BorgVisualLayers.LightStatus, false);
            return;
        }

        var moving = TryComp(ent, out SpriteMovementComponent? movement) && movement.IsMoving;

        _appearance.TryGetData(ent, BorgVisuals.HasPlayer, out bool hasPlayer);
        var eyeState = hasPlayer ? type.SpriteHasMindState : type.SpriteNoMindState;
        var lightState = type.SpriteToggleLightState;

        if (moving)
        {
            SetState(ent, BorgVisualLayers.Body, WalkVariant(ent, prefix, prefix));
            SetState(ent, BorgVisualLayers.Light, WalkVariant(ent, prefix, eyeState));
            SetState(ent, BorgVisualLayers.LightStatus, WalkVariant(ent, prefix, lightState));
        }
        else
        {
            SetState(ent, BorgVisualLayers.Body, prefix);
            SetState(ent, BorgVisualLayers.Light, eyeState);
            SetState(ent, BorgVisualLayers.LightStatus, lightState);
        }

        SyncAnimation(ent);
    }

    /// <summary>
    /// Turns e.g. <c>k9_e</c> into <c>k9_walk_e</c>, or returns the original state if no walk variant exists.
    /// </summary>
    private string WalkVariant(Entity<SpriteComponent> ent, string prefix, string state)
    {
        if (!state.StartsWith(prefix))
            return state;

        var walk = $"{prefix}_walk{state[prefix.Length..]}";
        return HasState(ent, BorgVisualLayers.Body, walk) ? walk : state;
    }

    private void SetState(Entity<SpriteComponent> ent, BorgVisualLayers key, string state)
    {
        if (HasState(ent, key, state))
            _sprite.LayerSetRsiState(ent.AsNullable(), key, state);
    }

    /// <summary>
    /// Invisible layers don't animate, so a headlamp turned on mid-walk would be on the wrong frame.
    /// Snap the eye and headlamp layers to the body's animation time.
    /// </summary>
    private void SyncAnimation(Entity<SpriteComponent> ent)
    {
        if (!TryGetValidLayer(ent, BorgVisualLayers.Body, out var body))
            return;

        if (TryGetValidLayer(ent, BorgVisualLayers.Light, out var eyes))
            _sprite.LayerSetAnimationTime(eyes, body.AnimationTime);

        if (TryGetValidLayer(ent, BorgVisualLayers.LightStatus, out var light))
            _sprite.LayerSetAnimationTime(light, body.AnimationTime);
    }

    private bool TryGetValidLayer(Entity<SpriteComponent> ent, BorgVisualLayers key, out SpriteComponent.Layer layer)
    {
        if (_sprite.TryGetLayer(ent.AsNullable(), key, out var found, false)
            && found.ActualRsi is { } rsi
            && rsi.TryGetState(found.State, out _))
        {
            layer = found;
            return true;
        }

        layer = default!;
        return false;
    }

    private bool HasState(Entity<SpriteComponent> ent, BorgVisualLayers key, string state)
    {
        return _sprite.TryGetLayer(ent.AsNullable(), key, out var layer, false)
            && layer.ActualRsi is { } rsi
            && rsi.TryGetState(state, out _);
    }
}
