using Content.Server.Atmos.Components;
using Content.Shared._Floof.CCVars;
using Content.Shared._Floof.Flooding;
using Content.Shared.Atmos;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;

namespace Content.Server.Doors.Systems;

public sealed partial class FirelockSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private FloodLevelSystem _floodLevel = default!;

    private float _floodDepth;

    private void InitializeFlood()
    {
        Subs.CVar(_cfg, CCVars.FloodFirelockDepth, value => _floodDepth = value, true);
    }

    /// <summary>
    ///     Whether the liquid on the firelock's open sides differs by enough depth for it to be holding back a flood.
    ///     Sealed off sides don't count, and neither does a flood that already stands level on both sides.
    /// </summary>
    private bool CheckFlood(EntityUid uid, AirtightComponent airtight)
    {
        var xform = Transform(uid);
        if (!TryComp<MapGridComponent>(xform.ParentUid, out var grid))
            return false;

        var pos = _mapping.CoordinatesToTile(xform.ParentUid, grid, xform.Coordinates);
        var min = float.MaxValue;
        var max = float.MinValue;

        for (var i = 0; i < Atmospherics.Directions; i++)
        {
            var dir = (AtmosDirection) (1 << i);
            if (!airtight.AirBlockedDirection.HasFlag(dir))
                continue;

            var adjacent = pos.Offset(dir);
            if (HasAirtightBlocker(_mapping.GetAnchoredEntities(xform.ParentUid, grid, adjacent), dir.GetOpposite()))
                continue;

            var depth = GetFloodDepth(xform.ParentUid, grid, adjacent);
            min = Math.Min(min, depth);
            max = Math.Max(max, depth);
        }

        // Edge firelocks hold back their own tile.
        if (airtight.AirBlockedDirection != AtmosDirection.All)
        {
            var depth = GetFloodDepth(xform.ParentUid, grid, pos);
            min = Math.Min(min, depth);
            max = Math.Max(max, depth);
        }

        return max - min >= _floodDepth;
    }

    private float GetFloodDepth(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        var volume = _floodLevel.GetVolume(gridUid, grid, tile);
        return FloodLevelSystem.VolumeToDepth(volume);
    }
}
