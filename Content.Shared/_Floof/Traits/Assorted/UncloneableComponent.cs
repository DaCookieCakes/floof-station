// Originally from the Delta-v project. Copyright (c) Delta-v contributors.
// Moved to this project; original copyright remains with its holders.
// Licensed under the GNU Affero General Public License v3.0.
using Robust.Shared.GameStates;

namespace Content.Shared._Floof.Traits.Assorted;

/// <summary>
/// This entity cannot be cloned but can still be revived by defibrillators.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class UncloneableComponent : Component;
