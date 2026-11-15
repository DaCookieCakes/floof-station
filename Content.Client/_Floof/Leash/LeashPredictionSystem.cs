using Content.Shared._Floof.CCVars;
using Content.Shared._Floof.Leash.Components;
using Robust.Client.Physics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Client._Floof.Leash;

/// <summary>
///     Marks leashed entities as physics-predicted while the local player is holding their leash.
/// </summary>
/// <remarks>
///     The leash joint pulls the leashed entity into the local player's physics island, so the client simulates it regardless.
///     If it isn't marked as predicted, the client's simulation fights with server state interpolation,
///         causing the leashed entity to drift (e.g. float backwards when the holder stops).
/// </remarks>
public sealed partial class LeashPredictionSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private Robust.Client.Physics.PhysicsSystem _physics = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    private bool _predictLeashes;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(_ => RefreshAll());
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(_ => RefreshAll());

        _config.OnValueChanged(CCVars.PredictLeashes, OnPredictChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _config.UnsubValueChanged(CCVars.PredictLeashes, OnPredictChanged);
    }

    private void OnPredictChanged(bool value)
    {
        _predictLeashes = value;
        RefreshAll();
    }

    [SubscribeLocalEvent]
    private void OnUpdateIsPredicted(Entity<LeashedComponent> ent, ref UpdateIsPredictedEvent args)
    {
        if (!_predictLeashes
            || ent.Comp.JointId is null
            || _player.LocalEntity is not { } localEnt
            || ent.Owner == localEnt // Already predicted as the local player
            || !TryGetEntity(ent.Comp.Leash, out var leash))
            return;

        if (IsHeldBy(leash.Value, localEnt))
            args.IsPredicted = true;
    }

    private bool IsHeldBy(EntityUid leash, EntityUid holder)
    {
        // Check the whole container chain, so a leash inside a held bag also counts
        var current = leash;
        while (_container.TryGetContainingContainer(current, out var container))
        {
            if (container.Owner == holder)
                return true;

            current = container.Owner;
        }

        return false;
    }

    [SubscribeLocalEvent]
    private void OnStartup(Entity<LeashedComponent> ent, ref ComponentStartup args)
    {
        OnLeashedChanged(ent, ref args);
    }

    [SubscribeLocalEvent]
    private void OnShutdown(Entity<LeashedComponent> ent, ref ComponentRemove args)
    {
        OnLeashedChanged(ent, ref args);
    }

    [SubscribeLocalEvent]
    private void AfterLeashedHandleState(Entity<LeashedComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        OnLeashedChanged(ent, ref args);
    }

    private void OnLeashedChanged<T>(Entity<LeashedComponent> ent, ref T args)
    {
        _physics.UpdateIsPredicted(ent);
    }

    [SubscribeLocalEvent]
    private void AfterLeashHandleState(Entity<LeashComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        OnLeashMoved(ent, ref args);
    }

    private void OnLeashMoved<T>(Entity<LeashComponent> ent, ref T args)
    {
        foreach (var data in ent.Comp.Leashed)
        {
            if (TryGetEntity(data.Pulled, out var pulled))
                _physics.UpdateIsPredicted(pulled);
        }
    }

    private void RefreshAll()
    {
        var query = EntityQueryEnumerator<LeashedComponent>();
        while (query.MoveNext(out var uid, out _))
            _physics.UpdateIsPredicted(uid);
    }
}
