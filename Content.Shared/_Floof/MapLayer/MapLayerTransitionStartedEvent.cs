using System.Numerics;

namespace Content.Shared._Floof.MapLayer;

[ByRefEvent]
public readonly record struct MapLayerTransitionStartedEvent(EntityUid Layer, EntityUid From, EntityUid To, Vector2 Position);
