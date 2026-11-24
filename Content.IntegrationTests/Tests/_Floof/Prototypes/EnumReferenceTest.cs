using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Robust.Shared.Prototypes;
using Robust.Shared.Reflection;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.IntegrationTests.Tests._Floof.Prototypes;

[TestFixture]
public sealed class EnumReferenceTest : GameTest
{
    [SidedDependency(Side.Client)] private IPrototypeManager _cProtoMan = default!;
    [SidedDependency(Side.Client)] private IReflectionManager _cReflection = default!;

    [Test]
    [Description("Every enum.X.Y string in entity prototype YAML resolves to an existing enum value.")]
    public async Task EntityEnumReferencesResolve()
    {
        var broken = new List<string>();

        await Client.WaitAssertion(() =>
        {
            foreach (var proto in _cProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (!_cProtoMan.TryGetMapping<EntityPrototype>(proto.ID, out var mapping))
                    continue;

                CollectBrokenReferences(proto.ID, mapping, broken);
            }
        });

        Assert.That(broken, Is.Empty);
    }

    private void CollectBrokenReferences(string protoId, DataNode node, List<string> broken)
    {
        switch (node)
        {
            case ValueDataNode value:
                CheckReference(protoId, value.Value, broken);
                break;
            case SequenceDataNode sequence:
                foreach (var child in sequence.Sequence)
                {
                    CollectBrokenReferences(protoId, child, broken);
                }
                break;
            case MappingDataNode mapping:
                foreach (var (key, child) in mapping)
                {
                    CheckReference(protoId, key, broken);
                    CollectBrokenReferences(protoId, child, broken);
                }
                break;
        }
    }

    private void CheckReference(string protoId, string reference, List<string> broken)
    {
        if (!reference.StartsWith("enum."))
            return;

        bool resolved;
        try
        {
            resolved = _cReflection.TryParseEnumReference(reference, out _, shouldThrow: false);
        }
        catch (ArgumentException)
        {
            resolved = false;
        }

        if (!resolved)
            broken.Add($"{protoId}: {reference}");
    }
}
