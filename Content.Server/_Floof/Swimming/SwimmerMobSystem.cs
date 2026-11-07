using System.Diagnostics.CodeAnalysis;
using Stopwatch = System.Diagnostics.Stopwatch;
using Content.Shared.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared._Floof.CCVars;
using Content.Shared._Floof.Flooding;
using Content.Shared._Floof.Swimming;
using Content.Shared.Atmos;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Floof.Swimming;

/// <summary>
///     What deep liquid does to the mobs in it: no air to breathe once their head is under, tiring them out while they
///     swim, reagents working on their skin, and swallowing some of it.
/// </summary>
public sealed partial class SwimmerMobSystem : EntitySystem
{
    [Dependency] private AtmosphereSystem _atmos = default!;
    [Dependency] private BodySystem _body = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private FloodLevelSystem _level = default!;
    [Dependency] private SwimmingSystem _swimming = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private InternalsSystem _internals = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ReactiveSystem _reactive = default!;
    [Dependency] private RespiratorSystem _respirator = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedStaminaSystem _stamina = default!;
    [Dependency] private StomachSystem _stomach = default!;
    [Dependency] private IGameTiming _timing = default!;

    [Dependency] private EntityQuery<FlammableComponent> _flammableQuery = default!;
    [Dependency] private EntityQuery<RespiratorComponent> _respiratorQuery = default!;

    private const float Interval = 1f;

    private static readonly ProtoId<ReagentPrototype> Saline = "Saline";

    private readonly Stopwatch _stopwatch = new();

    private float _accumulator;
    private float _extinguishDepth;
    private float _swimStamina;
    private float _touchRate;
    private float _swallowRate;
    private float _strokeInterval;

    [ViewVariables]
    public long ElapsedMicroseconds { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SwimmerComponent, InhaleLocationEvent>(OnInhale, after: new[] { typeof(InternalsSystem) });

        Subs.CVar(_cfg, CCVars.FloodExtinguishDepth, value => _extinguishDepth = value, true);
        Subs.CVar(_cfg, CCVars.FloodSwimStamina, value => _swimStamina = value, true);
        Subs.CVar(_cfg, CCVars.FloodTouchRate, value => _touchRate = value, true);
        Subs.CVar(_cfg, CCVars.FloodSwallowRate, value => _swallowRate = value, true);
        Subs.CVar(_cfg, CCVars.FloodStrokeInterval, value => _strokeInterval = value, true);
    }

    private void OnInhale(Entity<SwimmerComponent> ent, ref InhaleLocationEvent args)
    {
        // Internals and the like have already picked what to breathe from.
        if (args.Gas == null && _swimming.IsSubmerged(ent, Transform(ent)))
            args.Gas = GetLiquidBreath(ent, args.Respirator, Transform(ent));
    }

    /// <summary>
    ///     What a breath of the liquid around the mob comes to. The lungs get the liquid's gas forms, so whoever can
    ///     breathe a gas can breathe its liquid too, and it's up to their metabolism who that is. Nothing for anyone else.
    /// </summary>
    private GasMixture GetLiquidBreath(EntityUid uid, RespiratorComponent respirator, TransformComponent xform)
    {
        var breath = new GasMixture(respirator.BreathVolume) { Temperature = Atmospherics.T20C };

        if (!TryGetLiquid(xform, out var solution, out _) || solution.Volume <= 0)
            return breath;

        var moles = Atmospherics.OneAtmosphere * respirator.BreathVolume / (Atmospherics.R * Atmospherics.T20C);
        for (var i = 0; i < Atmospherics.TotalNumberOfGases; i++)
        {
            if (_atmos.GasReagents[i] is { } reagent)
                breath.AdjustMoles(i, moles * (solution.GetTotalPrototypeQuantity(reagent) / solution.Volume).Float());
        }

        // Salt water has no gas form of its own, the gills take the water out of it.
        breath.AdjustMoles(Gas.WaterVapor, moles * (solution.GetTotalPrototypeQuantity(Saline) / solution.Volume).Float());

        return _respirator.CanMetabolizeInhaledAir((uid, respirator), breath)
            ? breath
            : new GasMixture(respirator.BreathVolume);
    }

    /// <summary>
    ///     The puddle the entity is in.
    /// </summary>
    private bool TryGetLiquid(TransformComponent xform, [NotNullWhen(true)] out Solution? solution, out Entity<SolutionComponent>? solutionEnt)
    {
        solution = null;
        solutionEnt = null;
        return _level.TryGetPuddle(xform, out var puddle) &&
               _solutions.ResolveSolution(puddle.Owner, puddle.Comp.SolutionName, ref solutionEnt, out solution);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < Interval)
            return;

        _accumulator -= Interval;
        _stopwatch.Restart();

        var query = EntityQueryEnumerator<SwimmerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var swimmer, out var xform))
        {
            if (!TryGetLiquid(xform, out var solution, out var solutionEnt))
                continue;

            var depth = _swimming.GetStandingDepth(uid, xform);
            if (depth <= 0f)
                continue;

            UpdateMob(uid, swimmer, xform, solution, solutionEnt, depth);
        }

        ElapsedMicroseconds = _stopwatch.ElapsedTicks * 1_000_000 / Stopwatch.Frequency;
    }

    private void UpdateMob(EntityUid uid, SwimmerComponent swimmer, TransformComponent xform, Solution solution, Entity<SolutionComponent>? solutionEnt, float depth)
    {
        if (depth >= _extinguishDepth &&
            _flammableQuery.TryComp(uid, out var flammable) && flammable.OnFire &&
            solution.GetSolutionFlammability(_proto) <= 0)
            _flammable.Extinguish(uid, flammable);

        _reactive.DoEntityReaction(uid, solution.Clone().SplitSolution(FixedPoint2.New(_touchRate * depth)), ReactionMethod.Touch);

        if (_mobState.IsDead(uid))
            return;

        // Only strokes tire you out, so treading water lets stamina come back.
        var sinceStroke = (_timing.CurTime - swimmer.LastStroke).TotalSeconds;
        if (depth >= _swimming.GetSwimDepth(uid) && !_swimming.IsOnBottom(uid) && sinceStroke <= Math.Max(Interval, _strokeInterval))
            _stamina.TakeStaminaDamage(uid, _swimStamina, visual: false);

        if (!_swimming.IsSubmerged(uid, xform) || !_respiratorQuery.TryComp(uid, out var respirator) || _internals.AreInternalsWorking(uid))
            return;

        // Gills pass it through instead.
        if (GetLiquidBreath(uid, respirator, xform).TotalMoles > 0f)
            return;

        if (!_body.TryGetOrgansWithComponent<StomachComponent>(uid, out var stomachs))
            return;

        var amount = FixedPoint2.New(_swallowRate);
        var swallowed = solution.Clone().SplitSolution(amount);
        foreach (var stomach in stomachs)
        {
            if (!_stomach.CanTransferSolution((stomach.Owner, stomach.Comp), swallowed))
                continue;

            _reactive.DoEntityReaction(uid, swallowed, ReactionMethod.Ingestion);
            _stomach.TryTransferSolution((stomach.Owner, stomach.Comp), swallowed);
            if (solutionEnt != null)
                _solutions.SplitSolution(solutionEnt.Value, amount);

            return;
        }
    }
}
