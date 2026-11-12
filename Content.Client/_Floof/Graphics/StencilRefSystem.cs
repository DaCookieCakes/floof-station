namespace Content.Client._Floof.Graphics;

/// <summary>
///     Hands out stencil values to overlays. One counter for all of them, so two overlays never mark with the same
///     value in one frame and mistake each other's marks for their own.
/// </summary>
public sealed partial class StencilRefSystem : EntitySystem
{
    /// <summary>
    ///     0 is the cleared buffer (or 255), 1 is used by the engine's stencil shaders.
    /// </summary>
    private const int MinStencilRef = 2;
    private const int MaxStencilRef = 254;

    private int _stencilRef = MinStencilRef;

    /// <summary>
    ///     A new value for each draw, so marks left in the stencil from earlier frames don't count either.
    /// </summary>
    public int Next()
    {
        _stencilRef = _stencilRef >= MaxStencilRef ? MinStencilRef : _stencilRef + 1;
        return _stencilRef;
    }
}
