using System.Numerics;

namespace Content.Client._Floof.Sonar;

[RegisterComponent]
public sealed partial class SonarViewComponent : Component
{
    [ViewVariables]
    public EntityUid? Sub;

    [ViewVariables]
    public readonly List<SonarBlip> Blips = new();

    [ViewVariables]
    public TimeSpan NextPassive;

    [ViewVariables]
    public TimeSpan NextPing;

    [ViewVariables]
    public SonarPing? Ping;
}

public readonly record struct SonarBlip(
    Vector2 Position,
    float? Z,
    TimeSpan Time,
    TimeSpan Lifetime,
    float Strength);

public sealed class SonarPing
{
    public required TimeSpan Start;
    public required Vector2 Center;
    public required float HullRadius;
    public readonly List<SonarTarget> Targets = new();

    public int Next;
}

public readonly record struct SonarTarget(
    EntityUid Grid,
    Vector2i Tile,
    Vector2 Position,
    EntityUid Map,
    float Distance);
