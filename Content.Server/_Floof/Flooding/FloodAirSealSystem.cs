using Content.Server.Atmos.EntitySystems;
using Content.Shared._Floof.Flooding;
using Content.Shared.Fluids.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._Floof.Flooding;

/// <summary>
///     A tile about full of liquid holds its air like a wall would, until it drains again.
/// </summary>
public sealed partial class FloodAirSealSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private SharedMapSystem _map = default!;

    /// <summary>
    ///     Share of a tile's volume that seals it against gas, and the share it opens up again below. They're apart so a
    ///     tile that's just about full doesn't keep rebuilding atmos around it.
    /// </summary>
    private const float SealShare = 0.95f;
    private const float UnsealShare = 0.85f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PuddleComponent, PuddleVolumeChangedEvent>(OnVolumeChanged);
        SubscribeLocalEvent<FloodAirSealedComponent, ComponentStartup>(OnSealStartup);
        SubscribeLocalEvent<FloodAirSealedComponent, ComponentShutdown>(OnSealShutdown);
    }

    private void OnVolumeChanged(Entity<PuddleComponent> ent, ref PuddleVolumeChangedEvent args)
    {
        var share = args.Volume.Float() / FloodConstants.TileVolume.Float();
        if (share >= SealShare)
            EnsureComp<FloodAirSealedComponent>(ent);
        else if (share < UnsealShare)
            RemCompDeferred<FloodAirSealedComponent>(ent);
    }

    private void OnSealStartup(Entity<FloodAirSealedComponent> ent, ref ComponentStartup args)
    {
        SetSeal(ent, true);
    }

    private void OnSealShutdown(Entity<FloodAirSealedComponent> ent, ref ComponentShutdown args)
    {
        SetSeal(ent, false);
    }

    private void SetSeal(EntityUid uid, bool full)
    {
        var xform = Transform(uid);
        if (TryComp<MapGridComponent>(xform.GridUid, out var grid))
            _atmos.SetFloodFull(xform.GridUid.Value, _map.TileIndicesFor(xform.GridUid.Value, grid, xform.Coordinates), full);
    }
}
