using Content.Shared._Floof.MapUnderlay;
using Robust.Shared.Timing;

namespace Content.Shared._Floof.Ocean;

/// <summary>
///     The floor below only shows through the water once it's close, and looks smaller further off.
/// </summary>
public sealed partial class OceanUnderlaySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OceanDepthComponent, MapUnderlayShowEvent>(OnShow);
    }

    private void OnShow(Entity<OceanDepthComponent> ent, ref MapUnderlayShowEvent args)
    {
        var landing = ent.Comp.GetLanding(_timing.CurTime, args.Range);
        // Same curve as the drawn floor.
        var share = Math.Clamp(landing / 0.4f, 0f, 1f);
        args.Share *= share * share * (3f - 2f * share);
        args.Zoom *= float.Lerp(ent.Comp.FloorFarZoom, 1f, ent.Comp.GetLanding(_timing.CurTime));
    }
}
