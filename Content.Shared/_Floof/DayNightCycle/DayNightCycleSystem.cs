using Content.Shared.GameTicking;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Floof.DayNightCycle;

/// <summary>
/// Colours the map light by time of day. Like upstream's light cycle, the server only sets the offset
/// and each client works out the colour itself.
/// </summary>
public sealed partial class DayNightCycleSystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        // After the sun shadows, they pick a random time of day otherwise.
        SubscribeLocalEvent<DayNightCycleComponent, MapInitEvent>(OnMapInit, after: [typeof(SharedSunShadowSystem)]);
    }

    private void OnMapInit(Entity<DayNightCycleComponent> ent, ref MapInitEvent args)
    {
        var cycle = _proto.Index(ent.Comp.Cycle);
        var length = cycle.Length.TotalSeconds;
        var roundTime = (_timing.CurTime - _ticker.RoundStartTimeSpan).TotalSeconds;

        var start = ent.Comp.StartTime ?? _random.NextFloat();
        var offset = (start * length - roundTime) % length;
        if (offset < 0)
            offset += length;

        ent.Comp.Offset = TimeSpan.FromSeconds(offset);
        Dirty(ent);

        if (TryComp<SunShadowCycleComponent>(ent, out var shadow))
        {
            shadow.Duration = cycle.Length;
            shadow.Offset = ent.Comp.Offset;
            Dirty(ent, shadow);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsServer || !_timing.IsFirstTimePredicted)
            return;

        var query = AllEntityQuery<DayNightCycleComponent, MapLightComponent>();
        while (query.MoveNext(out var uid, out var comp, out var light))
        {
            var cycle = _proto.Index(comp.Cycle);
            var length = cycle.Length.TotalSeconds;
            var seconds = (_timing.CurTime + comp.Offset - _ticker.RoundStartTimeSpan - _metadata.GetPauseTime(uid)).TotalSeconds;

            var time = (float) (seconds % length / length);
            if (time < 0)
                time += 1;

            light.AmbientLightColor = GetColor(cycle, time);
        }
    }

    public static Color GetColor(DayNightCyclePrototype cycle, float time)
    {
        var colors = cycle.Colors;
        for (var i = 0; i < colors.Count - 1; i++)
        {
            var from = colors[i];
            var to = colors[i + 1];
            if (time >= from.Time && time <= to.Time)
                return Blend(from.Color, to.Color, (time - from.Time) / (to.Time - from.Time));
        }

        // Between the last colour and the first, across midnight.
        var last = colors[^1];
        var first = colors[0];
        var since = time >= last.Time ? time - last.Time : time + 1 - last.Time;
        return Blend(last.Color, first.Color, since / (1 + first.Time - last.Time));
    }

    // Blended in linear space, so the colours in between don't go muddy.
    private static Color Blend(Color a, Color b, float t)
    {
        a = Color.FromSrgb(a);
        b = Color.FromSrgb(b);
        return Color.ToSrgb(new Color(
            MathHelper.Lerp(a.R, b.R, t),
            MathHelper.Lerp(a.G, b.G, t),
            MathHelper.Lerp(a.B, b.B, t)));
    }
}
