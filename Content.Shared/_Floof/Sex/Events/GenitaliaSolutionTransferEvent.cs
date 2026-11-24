using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;

namespace Content.Shared._Floof.Sex.Events;


[ByRefEvent]
public record struct  GenitaliaSolutionTransferEvent()
{
    public FixedPoint2 Amount = 50f;
    
    public required Entity<SolutionComponent> TargetSolution;

    public required string SourceSolution;

}