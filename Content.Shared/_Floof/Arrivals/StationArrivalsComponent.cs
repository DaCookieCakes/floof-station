// moved from space-wizards/space-station-14 Content.Shared/Shuttles/Components/StationArrivalsComponent.cs (MIT, see LICENSE-MIT.TXT).
// SPDX-License-Identifier: MIT AND AGPL-3.0-or-later

using Content.Shared.Shuttles.Systems;
using Robust.Shared.Utility;

namespace Content.Shared.Shuttles.Components;

/// <summary>
/// Added to a station that is available for arrivals shuttles.
/// </summary>
[RegisterComponent, Access(typeof(ArrivalsSystem))]
public sealed partial class StationArrivalsComponent : Component
{
    [DataField("shuttle")]
    public EntityUid Shuttle;

    [DataField("shuttlePath")] public ResPath ShuttlePath = new("/Maps/Shuttles/arrivals.yml");

    /// <summary>
    /// A whole map loaded as this station's own arrivals terminal, components and all.
    /// The grid with <c>BecomesTerminal</c> is the terminal.
    /// Null means the station has no arrivals terminal or shuttle.
    /// </summary>
    [DataField]
    public ResPath? ArrivalsMap;
}
