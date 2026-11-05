// Originally from the Delta-v project. Copyright (c) Delta-v contributors.
// Moved to this project; original copyright remains with its holders.
// Licensed under the GNU Affero General Public License v3.0.
using Content.Shared.Body;
using Content.Shared.Examine;
using Content.Shared.Movement.Components; // TODO: use BrainComponent instead of InputMover when shitmed is merged


namespace Content.Shared._Floof.Traits.Assorted;

/// <summary>
/// Adds a warning examine message to brains with <see cref="UnborgableComponent"/>.
/// </summary>
public sealed partial class UnborgableSystem : EntitySystem
{
    [Dependency] private BodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UnborgableComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<UnborgableComponent, ExaminedEvent>(OnExamined);
    }

    /// <summary>
    /// Returns true if a mob's brain has <see cref="UnborgableComponent"/>.
    /// </summary>
    public bool IsUnborgable(Entity<BodyComponent?> ent)
    {
        // technically this will apply for any organ not just brain, but assume nobody will be evil and do that
        _body.TryGetOrgansWithComponent<UnborgableComponent>(ent, out var organs);
        return organs.Count > 0;
    }

    private void OnMapInit(Entity<UnborgableComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<BodyComponent>(ent, out var body))
            return;

        _body.TryGetOrgansWithComponent<InputMoverComponent>((ent.Owner, body), out var brains);
        foreach (var brain in brains)
        {
            EnsureComp<UnborgableComponent>(brain);
        }
    }

    private void OnExamined(Entity<UnborgableComponent> ent, ref ExaminedEvent args)
    {
        // need a health analyzer to see if someone can't be borged, can't just look at them and know
        if (!args.IsInDetailsRange || HasComp<BodyComponent>(ent))
            return;

        args.PushMarkup(Loc.GetString("brain-cannot-be-borged-message"));
    }
}
