using Content.Shared.Buckle.Components;
using Content.Shared.Rotation;

namespace Content.Shared._Floof.Buckle;

public sealed partial class DirectionalBedSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedRotationVisualsSystem _rotationVisuals = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DirectionalBedComponent, StrappedEvent>(OnStrapped);
    }

    private void OnStrapped(Entity<DirectionalBedComponent> ent, ref StrappedEvent args)
    {
        var rotation = Transform(ent).LocalRotation;
        _transform.SetLocalRotationNoLerp(args.Buckle, -rotation);

        if (rotation.GetCardinalDir() is Direction.North or Direction.East)
            _rotationVisuals.SetHorizontalAngle(args.Buckle.Owner, -args.Strap.Comp.Rotation);
    }
}
