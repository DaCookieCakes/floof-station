// copied from ss14Starlight/space-station-14 Content.Shared/_Starlight/ChildEntities/ChildEntitiesComponent.cs (Starlight License, see LICENSE-Starlight.txt).
// SPDX-License-Identifier: LicenseRef-Starlight AND AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Server._Floof.ChildEntities;

[RegisterComponent, Access(typeof(AnchoredChildEntitiesSystem))]
public sealed partial class AnchoredChildEntitiesComponent : Component
{
    [DataField]
    public List<AnchoredChildEntityInfo> ChildPrototypes = [];

    [ViewVariables]
    public List<EntityUid> Children = [];
}

[DataDefinition]
public sealed partial class AnchoredChildEntityInfo
{
    [DataField]
    public EntProtoId Prototype;

    [DataField]
    public Vector2 Offset;
}
