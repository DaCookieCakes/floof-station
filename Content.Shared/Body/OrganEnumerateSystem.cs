namespace Content.Shared.Body;

public sealed partial class OrganEnumerateSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganComponent, BodyRelayedEvent<OrganEnumerate>>(Enumerate);
    }

    private void Enumerate(Entity<OrganComponent> ent, ref BodyRelayedEvent<OrganEnumerate> args)
    {
        args.Args.Organs.Add(ent);
    }
}
[ByRefEvent]
public record struct OrganEnumerate(List<Entity<OrganComponent>> Organs);