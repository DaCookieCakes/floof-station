using System.Numerics;
using Content.Shared._Floof.Leash.Components;
using Content.Shared._Floof.Util;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client._Floof.Leash;

public sealed class LeashVisualsOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    /// <summary>
    ///     Leashes longer than this many segments are considered broken (e.g. stale positions) and are not drawn.
    /// </summary>
    private const int MaxSegments = 16;

    private readonly IEntityManager _entMan;
    private readonly IGameTiming _timing;
    private readonly SpriteSystem _sprites;
    private readonly SharedTransformSystem _xform;
    private readonly SharedContainerSystem _container;

    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly EntityQuery<SpriteComponent> _spriteQuery;

    private ISawmill Log => Logger.GetSawmill("leash-visuals");
    private Ticker _logTicker = new(TimeSpan.FromSeconds(3));

    public LeashVisualsOverlay(IEntityManager entMan)
    {
        _entMan = entMan;
        _timing = IoCManager.Resolve<IGameTiming>();
        _sprites = _entMan.System<SpriteSystem>();
        _xform = _entMan.System<SharedTransformSystem>();
        _container = _entMan.System<SharedContainerSystem>();
        _xformQuery = _entMan.GetEntityQuery<TransformComponent>();
        _spriteQuery = _entMan.GetEntityQuery<SpriteComponent>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var worldHandle = args.WorldHandle;
        worldHandle.SetTransform(Matrix3x2.Identity);

        var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var query = _entMan.EntityQueryEnumerator<LeashedVisualsComponent>();
        while (query.MoveNext(out var visualsComp))
        {
            if (visualsComp.Source is not {Valid: true} source
                || visualsComp.Target is not {Valid: true} target
                || visualsComp.Sprite is null
                || !_xformQuery.TryGetComponent(source, out var sourceXform)
                || !_xformQuery.TryGetComponent(target, out var targetXform)
                || sourceXform.MapID != args.MapId
                || targetXform.MapID != sourceXform.MapID)
                continue;

            // If the leash is held by the leashed entity itself (in hand, in a bag, etc.), don't render anything.
            if (_container.TryGetOuterContainer(source, sourceXform, out var outer) && outer.Owner == target)
                continue;

            var posA = GetAnchorPosition((source, sourceXform), visualsComp.OffsetSource, eyeRotation);
            var posB = GetAnchorPosition((target, targetXform), visualsComp.OffsetTarget, eyeRotation);
            var diff = posB - posA;
            var length = diff.Length();

            // If both ends are in the same spot, don't render anything. This also guards against NaN directions below.
            if (!float.IsFinite(length) || length < 0.01f)
                continue;

            var texture = _sprites.Frame0(visualsComp.Sprite);
            var width = texture.Width / (float) EyeManager.PixelsPerMeter;
            var maxSegmentLength = texture.Height / (float) EyeManager.PixelsPerMeter;
            var segmentCount = Math.Max(1, (int) Math.Ceiling(length / maxSegmentLength));

            // Sanity check. Skip only this leash - other leashes should still render.
            if (segmentCount > MaxSegments)
            {
                if (_logTicker.TryUpdate(_timing))
                    Log.Warning($"Tried to render a leash joint with absurd length {length} between {_entMan.ToPrettyString(source)} and {_entMan.ToPrettyString(target)}.");
                continue;
            }

            var direction = diff / length;
            var angle = diff.ToWorldAngle();

            for (var i = 0; i < segmentCount; i++)
            {
                var segmentStart = posA + direction * (maxSegmentLength * i);
                var isLast = i == segmentCount - 1;
                var segmentLength = isLast
                    ? Math.Clamp(length - maxSegmentLength * i, 0f, maxSegmentLength)
                    : maxSegmentLength;

                // Find the midpoint, create a box that describes the sprite boundaries, then rotate it around the midpoint.
                // The local +Y of the box (texture top) faces posA, and -Y faces posB.
                var segmentMidPoint = segmentStart + direction * (segmentLength / 2f);
                var box = new Box2(-width / 2f, -segmentLength / 2f, width / 2f, segmentLength / 2f);
                var rotated = new Box2Rotated(box.Translated(segmentMidPoint), angle, segmentMidPoint);

                if (!isLast || segmentLength >= maxSegmentLength)
                {
                    worldHandle.DrawTextureRect(texture, rotated);
                    continue;
                }

                // The last segment is only partially drawn: take the top part of the texture so it continues
                // seamlessly from the previous segment. UIBox2 requires left <= right and top <= bottom.
                var regionHeight = Math.Clamp(segmentLength * EyeManager.PixelsPerMeter, 0f, texture.Height);
                var uv = new UIBox2(0, 0, texture.Width, regionHeight);
                worldHandle.DrawTextureRectRegion(texture, rotated, subRegion: uv);
            }
        }
    }

    /// <summary>
    ///     Returns the world position of the point the leash should be attached to on the given entity.
    /// </summary>
    private Vector2 GetAnchorPosition(Entity<TransformComponent> ent, Vector2 offset, Angle eyeRotation)
    {
        var (worldPos, worldRot) = _xform.GetWorldPositionRotation(ent.Comp);

        // Entities inside containers (e.g. a leash held in hand) aren't drawn, so their sprite data is irrelevant.
        if (_container.IsEntityInContainer(ent) || !_spriteQuery.TryGetComponent(ent, out var sprite))
            return worldPos + worldRot.RotateVec(offset);

        offset = offset * sprite.Scale + sprite.Offset;

        // NoRotation sprites always face the viewport "up", regardless of the entity's rotation.
        // Regular sprites are drawn with the entity's world rotation plus their own rotation.
        var rotation = sprite.NoRotation
            ? -eyeRotation
            : worldRot + sprite.Rotation;

        return worldPos + rotation.RotateVec(offset);
    }
}
