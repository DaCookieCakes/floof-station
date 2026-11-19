using System.Numerics;
using Robust.Shared.Map.Components;

namespace Content.Server._Floof.GridPlacement;

public sealed partial class GridPlacementSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;

    private const float Step = 2f;

    private List<Entity<MapGridComponent>> _overlapping = new();

    public bool IsClear(Entity<MapGridComponent> grid, EntityUid map, Vector2 position, Angle rotation)
    {
        var bounds = new Box2Rotated(grid.Comp.LocalAABB.Translated(position), rotation, position);

        _overlapping.Clear();
        _map.FindGridsIntersecting(map, bounds, ref _overlapping, includeMap: false);
        foreach (var other in _overlapping)
        {
            if (other.Owner != grid.Owner)
                return false;
        }

        return true;
    }

    public bool TryFindClearSpot(Entity<MapGridComponent> grid,
        EntityUid map,
        Vector2 position,
        Angle rotation,
        out Vector2 spot,
        float maxDistance = 50f)
    {
        spot = position;
        if (IsClear(grid, map, position, rotation))
            return true;

        for (var radius = Step; radius <= maxDistance; radius += Step)
        {
            var tries = Math.Max(8, (int) MathF.Ceiling(MathF.Tau * radius / Step));
            for (var i = 0; i < tries; i++)
            {
                var angle = MathF.Tau * i / tries;
                spot = position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                if (IsClear(grid, map, spot, rotation))
                    return true;
            }
        }

        spot = position;
        return false;
    }
}
