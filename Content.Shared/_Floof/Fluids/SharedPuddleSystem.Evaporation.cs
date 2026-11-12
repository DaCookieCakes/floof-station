using System.Linq;
using Content.Shared._Floof.Flooding;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Content.Shared.Fluids.Components;
using Content.Shared.Chemistry.Reagent;

namespace Content.Shared.Fluids;

public abstract partial class SharedPuddleSystem
{
    private static readonly TimeSpan EvaporationCooldown = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Share of a full tile's volume a puddle has to be under to evaporate.
    /// </summary>
    private const float EvaporationShare = 0.01f;

    private void OnEvaporationMapInit(Entity<EvaporationComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextTick = _timing.CurTime + EvaporationCooldown;
        Dirty(ent);
    }

    private void UpdateEvaporation(EntityUid uid, Solution solution)
    {
        // Deep water would take hours to dry anyway, so it doesn't pay for the ticks.
        if (solution.Volume < FloodConstants.TileVolume * EvaporationShare &&
            solution.GetTotalPrototypeQuantity(GetEvaporatingReagents(solution)) > FixedPoint2.Zero)
        {
            if (_evaporationQuery.HasComp(uid))
                return;

            var evaporation = AddComp<EvaporationComponent>(uid);
            evaporation.NextTick = _timing.CurTime + EvaporationCooldown;
            Dirty<EvaporationComponent>((uid, evaporation));
            return;
        }

        RemComp<EvaporationComponent>(uid);
    }

    private void TickEvaporation()
    {
        var query = EntityQueryEnumerator<EvaporationComponent, PuddleComponent>();
        var curTime = _timing.CurTime;
        while (query.MoveNext(out var uid, out var evaporation, out var puddle))
        {
            if (evaporation.NextTick > curTime)
                continue;

            // Necessary to keep client and server in sync so they don't drift
            evaporation.NextTick += EvaporationCooldown;
            Dirty(uid, evaporation);

            if (!_solutionContainerSystem.ResolveSolution(uid, puddle.SolutionName, ref puddle.Solution, out var puddleSolution))
                continue;

            // Most floods are one reagent, skip the lookups below.
            if (puddleSolution.Contents.Count == 1)
            {
                var single = puddleSolution.Contents[0].Reagent;
                var speed = ProtoMan.Index<ReagentPrototype>(single.Prototype).EvaporationSpeed;
                if (speed <= FixedPoint2.Zero)
                    continue;

                puddleSolution.RemoveReagent(single, evaporation.EvaporationAmount * EvaporationCooldown.TotalSeconds * speed);
                FinishEvaporationTick((uid, evaporation, puddle), puddle.Solution.Value, puddleSolution);
                continue;
            }

            // If we have multiple evaporating reagents in one puddle, just take the average evaporation speed and apply
            // that to all of them.
            var evaporationSpeeds = GetEvaporationSpeeds(puddleSolution);
            if (evaporationSpeeds.Count == 0)
                continue;

            // Can't use .Average because FixedPoint2
            var evaporationSpeed = evaporationSpeeds.Values.Sum() / evaporationSpeeds.Count;
            var reagentProportions = evaporationSpeeds.ToDictionary(kv => kv.Key,
                kv => puddleSolution.GetTotalPrototypeQuantity(kv.Key) / puddleSolution.Volume);

            // Still have to iterate over one-by-one since the full solution could have non-evaporating solutions.
            foreach (var (reagent, factor) in reagentProportions)
            {
                var reagentTick = evaporation.EvaporationAmount * EvaporationCooldown.TotalSeconds * evaporationSpeed * factor;
                puddleSolution.SplitSolutionWithOnly(reagentTick, reagent);
            }

            FinishEvaporationTick((uid, evaporation, puddle), puddle.Solution.Value, puddleSolution);
        }
    }

    private void FinishEvaporationTick(Entity<EvaporationComponent, PuddleComponent> ent, Entity<SolutionComponent> solutionEnt, Solution solution)
    {
        // Despawn if we're done
        if (solution.Volume == FixedPoint2.Zero)
        {
            // Spawn a *sparkle*
            if (_net.IsServer) // TODO: Change this once we have entity spawn prediction V2
                SpawnAttachedTo(ent.Comp1.EvaporationEffect, Transform(ent).Coordinates);
            QueueDel(ent);
        }

        OnPuddleEvaporated(solutionEnt, ent);
    }

    /// <summary>
    ///     The server batches these, a deep flood loses far too little per second to update every time.
    /// </summary>
    protected virtual void OnPuddleEvaporated(Entity<SolutionComponent> solution, EntityUid puddle)
    {
        _solutionContainerSystem.UpdateChemicals(solution);
    }


    public ProtoId<ReagentPrototype>[] GetEvaporatingReagents(Solution solution)
    {
        List<ProtoId<ReagentPrototype>> evaporatingReagents = [];
        foreach (var solProto in solution.GetReagentPrototypes(ProtoMan).Keys)
        {
            if (solProto.EvaporationSpeed > FixedPoint2.Zero)
                evaporatingReagents.Add(solProto.ID);
        }
        return evaporatingReagents.ToArray();
    }

    public ProtoId<ReagentPrototype>[] GetAbsorbentReagents(Solution solution)
    {
        var absorbentReagents = new List<ProtoId<ReagentPrototype>>();
        foreach (ReagentPrototype solProto in solution.GetReagentPrototypes(ProtoMan).Keys)
        {
            if (solProto.Absorbent)
                absorbentReagents.Add(solProto.ID);
        }
        return absorbentReagents.ToArray();
    }

    public bool CanFullyEvaporate(Solution solution)
    {
        return solution.GetTotalPrototypeQuantity(GetEvaporatingReagents(solution)) == solution.Volume;
    }

    /// <summary>
    /// Gets a mapping of evaporating speed of the reagents within a solution.
    /// The speed at which a solution evaporates is the average of the speed of all evaporating reagents in it.
    /// </summary>
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> GetEvaporationSpeeds(Solution solution)
    {
        Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> evaporatingSpeeds = [];
        foreach (var solProto in solution.GetReagentPrototypes(ProtoMan).Keys)
        {
            if (solProto.EvaporationSpeed > FixedPoint2.Zero)
            {
                evaporatingSpeeds.Add(solProto.ID, solProto.EvaporationSpeed);
            }
        }
        return evaporatingSpeeds;
    }
}
