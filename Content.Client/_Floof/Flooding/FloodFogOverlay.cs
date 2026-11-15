using System.Numerics;
using Content.Shared._Floof.Flooding;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     With the local player under, thick liquid hides what's further away. Only inside the liquid: its walls fade with it,
///     anything past those keeps the plain tint. Water at <see cref="ClearOpacity"/> stays clear.
/// </summary>
public sealed partial class FloodFogOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<ShaderPrototype> Shader = "FloodFog";

    private const float ClearOpacity = 0.55f;

    private static readonly Vector2i[] Directions = [new(0, 1), new(1, 0), new(0, -1), new(-1, 0)];

    private readonly FloodWaterlineSystem _system;
    private readonly FloodSubmergedSystem _submerged;
    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _xform;
    private readonly ShaderInstance _shader;

    private readonly Dictionary<Vector2i, Color> _barriers = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    public FloodFogOverlay()
    {
        IoCManager.InjectDependencies(this);
        _system = _entManager.System<FloodWaterlineSystem>();
        _submerged = _entManager.System<FloodSubmergedSystem>();
        _map = _entManager.System<SharedMapSystem>();
        _xform = _entManager.System<SharedTransformSystem>();
        _shader = _proto.Index(Shader).InstanceUnique();

        // After the caustics, before the screen tint.
        ZIndex = -1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _submerged.SubmergedView > 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye is not { } eye)
            return;

        var handle = args.WorldHandle;
        var center = args.Viewport.WorldToLocal(eye.Position.Position);
        var scale = (args.Viewport.WorldToLocal(eye.Position.Position + Vector2.UnitX) - center).Length();

        _shader.SetParameter("center", new Vector2(center.X, args.Viewport.Size.Y - center.Y));
        _shader.SetParameter("tile_pixels", scale);
        _shader.SetParameter("tint", _submerged.SubmergedTint);
        handle.UseShader(_shader);

        foreach (var (gridUid, tiles) in _system.Grids)
        {
            if (!_entManager.TryGetComponent<MapGridComponent>(gridUid, out var grid))
                continue;

            var (_, _, matrix, invMatrix) = _xform.GetWorldPositionRotationMatrixWithInv(gridUid);
            handle.SetTransform(matrix);
            var bounds = invMatrix.TransformBox(args.WorldBounds).Enlarged(1f);

            _barriers.Clear();
            foreach (var (indices, tile) in tiles)
            {
                if (tile.Depth <= 0f || !bounds.Contains(indices))
                    continue;

                var thickness = GetThickness(tile.Opacity);
                if (thickness <= 0f)
                    continue;

                var color = new Color(thickness, 0f, 0f, tile.Depth * _submerged.SubmergedView);
                DrawTile(handle, indices, color);

                foreach (var dir in Directions)
                {
                    var next = indices + dir;
                    if (!tiles.ContainsKey(next) && (!_barriers.TryGetValue(next, out var old) || old.A < color.A) && IsBarrier(gridUid, grid, next))
                        _barriers[next] = color;
                }
            }

            foreach (var (indices, color) in _barriers)
            {
                DrawTile(handle, indices, color);
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(null);
    }

    private static float GetThickness(float opacity)
    {
        return Math.Clamp((opacity - ClearOpacity) / (1f - ClearOpacity), 0f, 1f);
    }

    private static void DrawTile(DrawingHandleWorld handle, Vector2i indices, Color color)
    {
        handle.DrawRect(new Box2(indices, indices + Vector2i.One), color);
    }

    private bool IsBarrier(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var ent))
        {
            if (ent is { } uid && !_entManager.HasComponent<FloodDryComponent>(uid) && _system.GetBarrier(uid) != FloodBarrier.None)
                return true;
        }

        return false;
    }
}
