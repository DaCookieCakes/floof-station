// Originally from the Delta-v project. Copyright (c) Delta-v contributors.
// Moved to this project; original copyright remains with its holders.
// Licensed under the GNU Affero General Public License v3.0.
using Robust.Shared.GameStates;

namespace Content.Shared._Floof.Traits.Assorted;

/// <summary>
/// This is used for the unborgable trait, which blacklists a brain from MMIs.
/// If this is added to a body, it gets moved to its brain if it has one.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class UnborgableComponent : Component;
