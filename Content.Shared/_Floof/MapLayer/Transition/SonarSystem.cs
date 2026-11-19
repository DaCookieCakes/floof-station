namespace Content.Shared._Floof.MapLayer.Transition;

public sealed partial class SonarSystem : EntitySystem
{
    [SubscribeLocalEvent]
    private void OnToggle(EntityUid uid, SonarComponent comp, SonarToggleMessage args)
    {
        comp.Active = !comp.Active;
        Dirty(uid, comp);
    }
}
