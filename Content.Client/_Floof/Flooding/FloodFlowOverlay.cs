using System.Numerics;
using Content.Shared._Floof.CCVars;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     Draws faint streaks running with the current over moving liquid. Each fades in and out inside its own tile,
///     so a neighbour's streaks can point somewhere else without a visible seam.
/// </summary>
public sealed partial class FloodFlowOverlay : Overlay
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entManager = default!;

    private const int StreaksPerTile = 3;
    private const float StreakLength = 9f / 32f;

    /// <summary>
    ///     Keeps streaks away from the tile's edge sideways, and lets them run almost its full width lengthways.
    /// </summary>
    private const float SpreadAcross = 0.8f;
    private const float TravelAlong = 0.9f;
    private const float Margin = 1f / 64f;

    private readonly FloodWaterlineSystem _system;
    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _xform;

    private List<Entity<MapGridComponent>> _grids = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public FloodFlowOverlay()
    {
        IoCManager.InjectDependencies(this);
        _system = _entManager.System<FloodWaterlineSystem>();
        _map = _entManager.System<SharedMapSystem>();
        _xform = _entManager.System<SharedTransformSystem>();

        // On the water, under everything standing in it.
        ZIndex = (int) DrawDepth.Puddles + 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var opacity = _cfg.GetCVar(CCVars.FloodFlowStreakOpacity);

        _grids.Clear();
        _map.FindGridsIntersecting(args.MapId, args.WorldAABB, ref _grids);

        foreach (var grid in _grids)
        {
            if (!_system.Grids.TryGetValue(grid, out var tiles) || tiles.Count == 0)
                continue;

            var (_, _, matrix, invMatrix) = _xform.GetWorldPositionRotationMatrixWithInv(grid);
            handle.SetTransform(matrix);
            var bounds = invMatrix.TransformBox(args.WorldBounds).Enlarged(1f);

            foreach (var (indices, tile) in tiles)
            {
                var strength = tile.Flow.Length();
                if (strength <= 0.01f || !bounds.Contains(indices))
                    continue;

                var along = tile.Flow / strength;
                var across = new Vector2(-along.Y, along.X);
                var center = indices + Vector2Helpers.Half;
                var color = FloodWaterlineSystem.WaterlineColor(tile.Color);
                var alpha = opacity * MathF.Min(strength / FloodFlowSystem.StrongCurrent, 1f) * MathF.Min(tile.Depth / 0.1f, 1f);

                for (var i = 0; i < StreaksPerTile; i++)
                {
                    var seed = Seed(indices, i);
                    var phase = (tile.FlowPhase + seed) % 1f;
                    var middle = center +
                                 along * (phase - 0.5f) * TravelAlong +
                                 across * (Seed(indices, i + StreaksPerTile) - 0.5f) * SpreadAcross;

                    var start = Clamp(middle - along * StreakLength / 2f, indices);
                    var end = Clamp(middle + along * StreakLength / 2f, indices);
                    handle.DrawLine(start, end, color.WithAlpha(alpha * MathF.Sin(MathF.PI * phase)));
                }
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private static Vector2 Clamp(Vector2 point, Vector2i tile)
    {
        return new Vector2(
            Math.Clamp(point.X, tile.X + Margin, tile.X + 1f - Margin),
            Math.Clamp(point.Y, tile.Y + Margin, tile.Y + 1f - Margin));
    }

    /// <summary>
    ///     A number from 0 to 1 that's the same for this streak every frame.
    /// </summary>
    private static float Seed(Vector2i tile, int index)
    {
        var hash = unchecked((uint) (tile.X * 73856093 ^ tile.Y * 19349663 ^ (index + 1) * 83492791));
        hash ^= hash >> 13;
        hash = unchecked(hash * 1274126177u);
        return (hash >> 8) / (float) (1 << 24);
    }
}
