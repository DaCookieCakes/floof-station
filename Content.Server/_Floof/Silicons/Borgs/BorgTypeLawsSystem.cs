using Content.Server.Silicons.Laws;
using Content.Shared._Floof.Silicons.Borgs;

namespace Content.Server._Floof.Silicons.Borgs;

/// <summary>
/// Applies <see cref="BorgTypeLawsComponent.Laws"/> when the component is added, notifying the borg.
/// </summary>
public sealed partial class BorgTypeLawsSystem : EntitySystem
{
    [Dependency] private SiliconLawSystem _laws = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BorgTypeLawsComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<BorgTypeLawsComponent> ent, ref ComponentStartup args)
    {
        _laws.SetLaws(_laws.GetLawset(ent.Comp.Laws).Laws, ent);
    }
}
