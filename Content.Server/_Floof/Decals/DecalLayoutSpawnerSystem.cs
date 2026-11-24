using System.Numerics;
using Content.Server.Decals;
using Content.Shared._Floof.Decals;
using Robust.Shared.Map;

namespace Content.Server._Floof.Decals;

public sealed partial class DecalLayoutSpawnerSystem : EntitySystem
{
    [Dependency] private DecalSystem _decal = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DecalLayoutSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<DecalLayoutSpawnerComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } grid)
        {
            QueueDel(ent);
            return;
        }

        // Markers snap to the tile centre; decals are positioned by their bottom-left corner and rotate about their centre.
        var rotation = xform.LocalRotation.GetCardinalDir().ToAngle();
        var center = xform.Coordinates.Position;

        foreach (var entry in ent.Comp.Decals)
        {
            var proto = ProtoMan.Index(entry.Decal);
            var position = center + rotation.RotateVec(entry.Offset) - new Vector2(0.5f, 0.5f);

            _decal.TryAddDecal(
                entry.Decal,
                new EntityCoordinates(grid, position),
                out _,
                ent.Comp.Color,
                rotation,
                ent.Comp.ZIndex,
                ent.Comp.Cleanable ?? proto.DefaultCleanable);
        }

        QueueDel(ent);
    }
}
