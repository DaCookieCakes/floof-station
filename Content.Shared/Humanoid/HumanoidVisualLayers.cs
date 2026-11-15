using Content.Shared.Humanoid.Markings;
using Robust.Shared.Serialization;

namespace Content.Shared.Humanoid
{
    [Serializable, NetSerializable]
    public enum HumanoidVisualLayers : byte
    {
        Special, // for the cat ears
        Tail,
        TailOverlay, // markings that go ontop of tails
        Hair,
        FacialHair,
        UndergarmentTop,
        UndergarmentBottom,
        Chest,
        Head,
        Snout,
        SnoutCover, // things layered over snouts (i.e. noses)
        HeadSide, // side parts (i.e., frills)
        HeadTop,  // top parts (i.e., ears)
        Eyes,
        Eyelids,
        OverEyes,
        RArm,
        LArm,
        RHand,
        LHand,
        RLeg,
        LLeg,
        RFoot,
        LFoot,
        Overlay,
        Handcuffs,
        StencilMask,
        Ensnare,
        Fire,
        
        //Genitals - Floof
        Breasts,
        BreastsBehind,
        Testicles,
        TesticlesBehind,
        Penis,
        PenisBehind,
        Vagina,
        VaginaBehind, // This one is currently unused but adding it anyway for future support.

        // Floof Section - marking layering
        TailBehind, // behind the body
        TailBehindBackpack, // over outer clothing, under the backpack
        TailOversuit, // over the backpack
        // Floof Section End

    }
}
