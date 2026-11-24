using Content.Server.Fluids.EntitySystems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Chemistry.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using static Content.Shared._Floof.Flooding.FloodConstants;

namespace Content.Server._Floof.Flooding.Events;

/// <seealso cref="LiquidLeakRuleComponent"/>
public sealed partial class LiquidLeakRule : StationEventSystem<LiquidLeakRuleComponent>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PuddleSystem _puddle = default!;
    [Dependency] private SharedMapSystem _map = default!;

    protected override void Started(Entity<LiquidLeakRuleComponent, GameRuleComponent> ent, ref GameRuleStartedEvent args)
    {
        base.Started(ent, ref args);

        var leak = ent.Comp1;
        var gameRule = ent.Comp2;

        if (!TryComp<StationEventComponent>(ent, out var stationEvent))
            return;

        if (!Station.TryFindRandomTile(out var tile, out _, out var grid, out _) || PickProfile(leak) is not { } profile)
        {
            ForceEndSelf((ent, gameRule));
            return;
        }

        leak.TargetGrid = grid.Value;
        leak.TargetTile = tile;
        leak.Profile = profile;
        leak.LitresPerSecond = RobustRandom.NextFloat(profile.MinimumRate ?? leak.MinimumRate,
            profile.MaximumRate ?? leak.MaximumRate);

        var duration = RobustRandom.NextFloat(leak.MinimumDuration, leak.MaximumDuration)
            + (gameRule.Delay?.Next(RobustRandom) ?? 0f);
        stationEvent.EndTime = _timing.CurTime + TimeSpan.FromSeconds(duration);

        Sawmill.Info($"Liquid leak of {string.Join(", ", profile.Reagents.Keys)} at {tile} on {ToPrettyString(grid.Value)}, {leak.LitresPerSecond:0.#} L/s");
    }

    protected override void ActiveTick(EntityUid uid, LiquidLeakRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);
        component.TimeUntilLeak -= frameTime;

        if (component.TimeUntilLeak > 0f)
            return;
        component.TimeUntilLeak += component.LeakCooldown;

        if (component.Profile is not { } profile
            || !TryComp<MapGridComponent>(component.TargetGrid, out var grid)
            || !_map.TryGetTileRef(component.TargetGrid.Value, grid, component.TargetTile, out var tileRef))
        {
            ForceEndSelf((uid, gameRule));
            return;
        }

        var total = 0f;
        foreach (var ratio in profile.Reagents.Values)
        {
            total += ratio;
        }

        if (total <= 0f)
            return;

        var units = component.LitresPerSecond * component.LeakCooldown * UnitsPerLiter;
        var solution = new Solution();
        foreach (var (reagent, ratio) in profile.Reagents)
        {
            solution.AddReagent(reagent, units * ratio / total);
        }

        _puddle.TrySpillAt(tileRef, solution, out _, sound: false);
    }

    private LiquidLeakProfile? PickProfile(LiquidLeakRuleComponent leak)
    {
        var total = 0f;
        foreach (var profile in leak.Profiles)
        {
            total += profile.Weight;
        }

        var roll = RobustRandom.NextFloat(total);
        foreach (var profile in leak.Profiles)
        {
            roll -= profile.Weight;
            if (roll < 0f)
                return profile;
        }

        return null;
    }
}
