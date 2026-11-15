using Robust.Shared.Serialization.Markdown.Mapping;

namespace Content.Server._Floof.Persistence.Migrations;

/// <summary>
///     Upgrades a live character record from <see cref="FromVersion"/> to the next version.
///     Works on raw YAML since old versions no longer match <see cref="Content.Shared._Floof.Persistence.LiveCharacterRecord"/>.
/// </summary>
/// <remarks>
///     Implementations are found by reflection. Name them after the step, e.g. <c>LiveCharacterMigrationV1</c>.
/// </remarks>
public interface ILiveCharacterMigration
{
    int FromVersion { get; }

    void Migrate(MappingDataNode record);
}
