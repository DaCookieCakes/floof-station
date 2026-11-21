namespace Content.Shared._Floof.Sonar;

public abstract partial class SharedSonarSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnToggle(EntityUid uid, SonarComponent comp, SonarToggleMessage args)
    {
        comp.Active = !comp.Active;
        Dirty(uid, comp);
    }
}
