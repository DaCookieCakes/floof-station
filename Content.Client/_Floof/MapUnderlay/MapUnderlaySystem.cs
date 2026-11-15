using Content.Shared._Floof.CCVars;
using Content.Shared._Floof.MapUnderlay;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client._Floof.MapUnderlay;

/// <summary>
///     Works out the eye for the map shown beneath, and remembers which viewport is drawing on top of one so the
///     overlays there can leave it showing.
/// </summary>
public sealed partial class MapUnderlaySystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedMapSystem _map = default!;

    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<MapUnderlayComponent> _underlayQuery;
    private IClydeViewport? _drawing;
    private float _drawingShare;
    private Texture? _drawingUnlit;

    public override void Initialize()
    {
        base.Initialize();

        _mapQuery = GetEntityQuery<MapComponent>();
        _underlayQuery = GetEntityQuery<MapUnderlayComponent>();
    }

    /// <summary>
    ///     The eye that sees the map below the way <paramref name="eye"/> sees this one, if there's anything to see.
    ///     <paramref name="unlit"/> sees the same without the light down there, for the light from up here to go on.
    /// </summary>
    public bool TryGetEye(IEye eye, out IEye below, out IEye unlit, out float share)
    {
        below = default!;
        unlit = default!;
        share = 0f;

        if (!_cfg.GetCVar(CCVars.MapUnderlay) ||
            _map.GetMapOrInvalid(eye.Position.MapId) is not { Valid: true } map ||
            !_underlayQuery.TryComp(map, out var underlay) ||
            // Only there once the server sends it, which it does when it's close enough to show.
            !_mapQuery.TryComp(underlay.Map, out var belowMap))
            return false;

        // Not new(): a struct's empty constructor skips the defaults and leaves everything 0.
        var ev = new MapUnderlayShowEvent(1f);
        RaiseLocalEvent(map, ref ev);
        if (!(ev.Share > 0f) || !(ev.Zoom > 0f))
            return false;

        share = ev.Share;
        var position = new MapCoordinates(underlay.ToBelow(eye.Position.Position), belowMap.MapId);
        var rotation = eye.Rotation - underlay.Rotation;
        var offset = underlay.Rotation.RotateVec(eye.Offset);
        var zoom = eye.Zoom * ev.Zoom;
        below = new Robust.Shared.Graphics.Eye
        {
            Position = position,
            Rotation = rotation,
            Offset = offset,
            Zoom = zoom,
            DrawFov = false,
            DrawLight = eye.DrawLight,
        };
        unlit = new Robust.Shared.Graphics.Eye
        {
            Position = position,
            Rotation = rotation,
            Offset = offset,
            Zoom = zoom,
            DrawFov = false,
            DrawLight = false,
        };
        return true;
    }

    /// <summary>
    ///     Marks <paramref name="viewport"/> as drawing on top of the map below, until it's done.
    /// </summary>
    public void SetDrawing(IClydeViewport? viewport, float share = 0f, Texture? unlit = null)
    {
        _drawing = viewport;
        _drawingShare = share;
        _drawingUnlit = unlit;
    }

    /// <summary>
    ///     The map below as drawn without its own light, lined up with <paramref name="viewport"/>.
    /// </summary>
    public Texture? GetDrawnUnlit(IClydeViewport viewport)
    {
        return viewport == _drawing ? _drawingUnlit : null;
    }

    /// <summary>
    ///     How much of the map below shows through in this viewport right now, 0 if it isn't drawn.
    /// </summary>
    public float GetDrawnShare(IClydeViewport viewport)
    {
        return viewport == _drawing ? _drawingShare : 0f;
    }
}
