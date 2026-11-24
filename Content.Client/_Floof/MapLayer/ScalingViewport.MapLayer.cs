using System.Numerics;
using Content.Client._Floof.MapLayer;
using Robust.Client.Graphics;
using Robust.Shared.Graphics;

namespace Content.Client.Viewport;

public sealed partial class ScalingViewport
{
    [Dependency] private IOverlayManager _overlayManager = default!;

    private const string PlacementOverlayType = "Robust.Client.Placement.PlacementManager+PlacementOverlay";

    private MapLayerSystem? _layers;
    private Overlay? _placementOverlay;
    private IClydeViewport? _layerAbove;
    private readonly List<MapLayerView> _layersBelow = new();

    private bool RenderWithLayers(IClydeViewport viewport)
    {
        _layers ??= _entityManager.SystemOrNull<MapLayerSystem>();
        if (_eye is not { } eye ||
            _layers == null ||
            !_layers.TryGetViews(eye, GetViewBounds(viewport, eye), _layersBelow, out var above, out var belowRange))
        {
            FreeLayerAbove();
            return false;
        }

        var clear = viewport.ClearColor;
        try
        {
            var placement = HidePlacementOverlay();
            try
            {
                if (above is { } aboveView)
                    RenderLayerAbove(viewport, aboveView);
                else
                    FreeLayerAbove();

                RenderLayersBelow(viewport, belowRange);
            }
            finally
            {
                if (placement != null)
                    _overlayManager.AddOverlay(placement);
            }

            viewport.Eye = eye;
            var over = _layersBelow.Count > 0;
            if (over)
            {
                viewport.ClearColor = null;
                _layers.SetDrawingOver(viewport, MapLayerSystem.GetCover(0f, _layersBelow[^1].Gap, belowRange));
            }

            if (above is { } view && _layerAbove != null)
                _layers.SetDrawnAbove(viewport, _layerAbove.RenderTarget.Texture, view.Alpha);

            try
            {
                viewport.Render();
            }
            finally
            {
                _layers.SetDrawingOver(null);
                _layers.SetDrawnAbove(null);
            }
        }
        finally
        {
            viewport.Eye = eye;
            viewport.ClearColor = clear;
        }

        return true;
    }

    private static Box2 GetViewBounds(IClydeViewport viewport, IEye eye)
    {
        viewport.Eye = eye;
        var size = (Vector2) viewport.Size;
        var a = viewport.LocalToWorld(Vector2.Zero).Position;
        var b = viewport.LocalToWorld(new Vector2(size.X, 0f)).Position;
        var c = viewport.LocalToWorld(new Vector2(0f, size.Y)).Position;
        var d = viewport.LocalToWorld(size).Position;
        return new Box2(Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d)), Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d)));
    }

    private void RenderLayersBelow(IClydeViewport viewport, float belowRange)
    {
        var clear = viewport.ClearColor;
        for (var i = 0; i < _layersBelow.Count; i++)
        {
            var layer = _layersBelow[i];
            var first = i == 0;
            viewport.Eye = layer.Eye;
            viewport.ClearColor = first ? clear : null;
            if (!first)
                _layers!.SetDrawingOver(viewport, MapLayerSystem.GetCover(layer.Gap, _layersBelow[i - 1].Gap, belowRange));

            _layers!.SetDrawingOtherLayer(viewport, layer.Gap);
            try
            {
                viewport.Render();
            }
            finally
            {
                _layers.SetDrawingOver(null);
                _layers.SetDrawingOtherLayer(null);
            }
        }
    }

    private void RenderLayerAbove(IClydeViewport viewport, MapLayerView above)
    {
        if (_layerAbove == null || _layerAbove.Size != viewport.Size)
        {
            FreeLayerAbove();
            _layerAbove = _clyde.CreateViewport(viewport.Size, "MapLayerAbove");
        }

        _layerAbove.RenderScale = viewport.RenderScale;
        _layerAbove.Eye = above.Eye;
        _layerAbove.ClearColor = Color.Transparent;
        _layers!.SetDrawingOver(_layerAbove);
        _layers.SetDrawingOtherLayer(_layerAbove, above.Gap);
        try
        {
            _layerAbove.Render();
        }
        finally
        {
            _layers.SetDrawingOver(null);
            _layers.SetDrawingOtherLayer(null);
        }
    }

    private Overlay? HidePlacementOverlay()
    {
        if (_placementOverlay == null)
        {
            foreach (var overlay in _overlayManager.AllOverlays)
            {
                if (overlay.GetType().FullName != PlacementOverlayType)
                    continue;

                _placementOverlay = overlay;
                break;
            }
        }

        return _placementOverlay != null && _overlayManager.RemoveOverlay(_placementOverlay) ? _placementOverlay : null;
    }

    private void FreeLayerAbove()
    {
        _layerAbove?.Dispose();
        _layerAbove = null;
    }
}
