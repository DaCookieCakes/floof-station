using Content.Shared.Instruments;
using Robust.Shared.Prototypes;

namespace Content.Shared._EE.Traits.Assorted.Prototypes;

[Prototype("SingerInstrument")]
public sealed partial class SingerInstrumentPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     Configuration for SwappableInstrumentComponent.
    ///     string = display name of the instrument
    ///     byte 1 = instrument midi program
    ///     byte 2 = instrument midi bank
    /// </summary>
    [DataField(required: true)]
    public Dictionary<LocId, (byte, byte)> InstrumentList = new();

    /// <summary>
    ///     Instrument in <see cref="InstrumentList"/> that is used by default.
    /// </summary>
    [DataField(required: true)]
    public LocId DefaultInstrument = "";

    /// <summary>
    ///     The BUI configuration for the instrument.
    /// </summary>
    [DataField]
    public InstrumentUiKey? MidiUi;

    // The below is server only, as it uses a server-BUI event !type
    [DataField(serverOnly: true, required: true)]
    public EntProtoId MidiActionId;

    [DataField]
    public bool AllowPercussion;

    [DataField]
    public bool AllowProgramChange;
}
