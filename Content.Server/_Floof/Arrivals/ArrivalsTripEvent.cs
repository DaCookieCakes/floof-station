namespace Content.Server._Floof.Arrivals;

/// <summary>
/// Raised on an arrivals shuttle when it's due to leave for its next stop. Handle it to move the shuttle some other
/// way than FTL: call <c>ServerArrivalsSystem.TripDeparted</c> as it sets off (before moving it) and
/// <c>TripArrived</c> once it's docked at <see cref="Target"/>. The shuttle waits for that before its next trip.
/// </summary>
[ByRefEvent]
public record struct ArrivalsTripEvent(EntityUid Station, EntityUid Terminal, EntityUid Target, bool ToStation)
{
    public bool Handled;
    
    public TimeSpan TravelTime;
}
