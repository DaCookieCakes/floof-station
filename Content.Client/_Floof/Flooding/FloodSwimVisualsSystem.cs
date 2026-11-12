using System.Numerics;
using Content.Client._Starfall.Particles;
using Content.Shared._Floof.Flooding;
using Content.Shared._Floof.Swimming;
using Content.Shared._Starfall.Particles;
using Robust.Client.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Floof.Flooding;

/// <summary>
///     Lifts a swimmer out of the water with every stroke. The waterline comes from the entity's position,
///     not the sprite's, so the water stays put while the sprite rises through it. Strokes and wading also splash.
/// </summary>
public sealed partial class FloodSwimVisualsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FloodLevelSystem _level = default!;
    [Dependency] private SwimmingSystem _swimming = default!;
    [Dependency] private ParticleSystem _particles = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    private const float BobTime = 0.5f;
    private const float BobHeight = 3f / 32f;

    private static readonly ProtoId<ParticleEffectPrototype> StrokeSplash = "FloodSplashStroke";
    private static readonly ProtoId<ParticleEffectPrototype> WadeSplash = "FloodSplashWade";

    // Strokes older than this were made before the swimmer came into view.
    private const float SplashWindow = 0.2f;
    private static readonly TimeSpan WadeInterval = TimeSpan.FromSeconds(0.35);
    private const float WadeDepth = 0.1f;
    private const float WadeSpeed = 0.5f;

    // Other systems own the rest of the offset, so only our own share of it is swapped.
    private readonly Dictionary<EntityUid, float> _bobbing = new();

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<SwimmerComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var swimmer, out var sprite))
        {
            var since = (float) (curTime - swimmer.LastStroke).TotalSeconds;
            UpdateSplash(uid, swimmer, curTime, since);

            var bob = since is >= 0f and < BobTime ? MathF.Sin(MathF.PI * since / BobTime) * BobHeight : 0f;

            _bobbing.TryGetValue(uid, out var old);
            if (bob == old)
                continue;

            _sprite.SetOffset((uid, sprite), sprite.Offset + new Vector2(0f, bob - old));

            if (bob == 0f)
                _bobbing.Remove(uid);
            else
                _bobbing[uid] = bob;
        }
    }

    private void UpdateSplash(EntityUid uid, SwimmerComponent swimmer, TimeSpan curTime, float sinceStroke)
    {
        var stroked = swimmer.LastStroke > swimmer.LastSplash;
        if (!stroked && curTime < swimmer.NextWadeSplash)
            return;

        if (!_xformQuery.TryComp(uid, out var xform) || !_level.TryGetLiquidColor(xform, out var color))
            return;

        if (stroked)
        {
            swimmer.LastSplash = swimmer.LastStroke;
            if (sinceStroke < SplashWindow)
                Splash(StrokeSplash, uid, color);

            return;
        }

        swimmer.NextWadeSplash = curTime + WadeInterval;
        var depth = _swimming.GetStandingDepth(uid, xform);
        if (depth >= WadeDepth && (depth < _swimming.GetSwimDepth(uid) || _swimming.IsOnBottom(uid)) &&
            _physicsQuery.TryComp(uid, out var physics) && physics.LinearVelocity.LengthSquared() >= WadeSpeed * WadeSpeed)
            Splash(WadeSplash, uid, color);
    }

    private void Splash(ProtoId<ParticleEffectPrototype> effect, EntityUid uid, Color color)
    {
        _particles.CreateParticle(effect, uid, color.WithAlpha(1f), attach: false);
    }
}
