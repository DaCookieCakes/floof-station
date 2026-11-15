/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

// Adapted from Crystal Edge's z-level rendering (ScalingViewport.CEZLevels.cs).

using Content.Client._Floof.MapUnderlay;
using Robust.Client.Graphics;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    private MapUnderlaySystem? _underlay;
    private IClydeViewport? _underlayUnlit;

    /// <summary>
    ///     Draws the map below first, if this one has one showing, then this map on top without clearing it away.
    ///     The map below is also drawn unlit off screen, so the lights up here can be added onto it.
    /// </summary>
    private void RenderWithUnderlay(IClydeViewport viewport)
    {
        _underlay ??= _entityManager.SystemOrNull<MapUnderlaySystem>();
        if (_eye is not { } eye ||
            _underlay == null ||
            !_underlay.TryGetEye(eye, out var below, out var unlitEye, out var share))
        {
            _underlayUnlit?.Dispose();
            _underlayUnlit = null;
            viewport.Render();
            return;
        }

        if (_underlayUnlit == null || _underlayUnlit.Size != viewport.Size)
        {
            _underlayUnlit?.Dispose();
            _underlayUnlit = _clyde.CreateViewport(viewport.Size, "MapUnderlayUnlit");
        }

        var unlit = _underlayUnlit;
        unlit.RenderScale = viewport.RenderScale;
        unlit.Eye = unlitEye;
        unlit.Render();

        var clear = viewport.ClearColor;
        viewport.Eye = below;
        viewport.Render();

        viewport.Eye = eye;
        viewport.ClearColor = null;
        _underlay.SetDrawing(viewport, share, unlit.RenderTarget.Texture);
        try
        {
            viewport.Render();
        }
        finally
        {
            _underlay.SetDrawing(null);
            viewport.ClearColor = clear;
        }
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        _underlayUnlit?.Dispose();
        _underlayUnlit = null;
    }
}
