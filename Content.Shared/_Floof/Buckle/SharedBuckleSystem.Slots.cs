using System.Numerics;
using Content.Shared._Floof.Buckle;
using Content.Shared.Buckle.Components;

namespace Content.Shared.Buckle;

public abstract partial class SharedBuckleSystem
{
    private Vector2 GetBuckleOffset(Entity<StrapComponent> strap, EntityUid buckle)
    {
        if (!TryComp<StrapSlotsComponent>(strap, out var slots))
            return strap.Comp.BuckleOffset;

        foreach (var offset in slots.Offsets)
        {
            var slot = GetSlotOffset(strap, offset);
            var taken = false;
            foreach (var other in strap.Comp.BuckledEntities)
            {
                if (other == buckle || !IsAtOffset(Transform(other).LocalPosition, slot))
                    continue;

                taken = true;
                break;
            }

            if (!taken)
                return slot;
        }

        return strap.Comp.BuckleOffset;
    }

    private bool IsSlotOffset(EntityUid strap, Vector2 position)
    {
        if (!TryComp<StrapSlotsComponent>(strap, out var slots))
            return false;

        foreach (var offset in slots.Offsets)
        {
            if (IsAtOffset(position, GetSlotOffset(strap, offset)))
                return true;
        }

        return false;
    }

    private Vector2 GetSlotOffset(EntityUid strap, Vector2 offset)
    {
        if (!HasComp<DirectionalBedComponent>(strap))
            return offset;

        return (-Transform(strap).LocalRotation).RotateVec(offset);
    }

    private static bool IsAtOffset(Vector2 position, Vector2 offset)
    {
        return (position - offset).LengthSquared() <= 1e-5;
    }
}
