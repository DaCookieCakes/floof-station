using System.Linq;
using Content.Shared._Floof.AnimatedTiles;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.ContentPack;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Floof.AnimatedTiles;

public sealed partial class AnimatedTileSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;

    private TileAnimation?[]? _animations;
    private Texture?[]? _seeThrough;

    /// <summary>
    /// Indexed by tile type ID, null for tiles that don't animate.
    /// Built on first use, tiles may not be registered yet when this system initializes.
    /// </summary>
    public TileAnimation?[] Tiles => _animations ??= Load();

    /// <summary>
    /// Indexed by tile type ID, the sprite of tiles with any transparency (lattice, glass), null for solid ones.
    /// Animations under them draw over them, so they get drawn again on top.
    /// </summary>
    public Texture?[] SeeThrough => _seeThrough ??= LoadSeeThrough();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        _overlay.AddOverlay(new AnimatedTileOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<AnimatedTileOverlay>();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<AnimatedTilePrototype>())
            _animations = null;
    }

    private TileAnimation?[] Load()
    {
        var tiles = new TileAnimation?[_tiles.Count];

        foreach (var proto in _proto.EnumeratePrototypes<AnimatedTilePrototype>())
        {
            if (!_tiles.TryGetDefinition(proto.ID, out var def))
            {
                Log.Error($"Animated tile {proto.ID} has no tile with that ID.");
                continue;
            }

            var rsi = _cache.GetResource<RSIResource>(proto.Sprite).RSI;
            tiles[def.TileId] = new TileAnimation(rsi, proto.State);
        }

        return tiles;
    }

    private Texture?[] LoadSeeThrough()
    {
        var seeThrough = new Texture?[_tiles.Count];

        foreach (var def in _tiles)
        {
            if (def.Sprite is not { } sprite)
                continue;

            using var stream = _resources.ContentFileRead(sprite);
            using var image = Image.Load<Rgba32>(stream);
            if (HasTransparency(image))
                seeThrough[def.TileId] = Texture.LoadFromImage(image, sprite.ToString());
        }

        return seeThrough;
    }

    private static bool HasTransparency(Image<Rgba32> image)
    {
        foreach (var pixel in image.GetPixelSpan())
        {
            if (pixel.A < byte.MaxValue)
                return true;
        }

        return false;
    }
}

public sealed class TileAnimation
{
    private readonly Texture[] _frames;
    private readonly float[] _delays;
    private readonly float _length;

    public TileAnimation(RSI rsi, string state)
    {
        var rsiState = rsi[state];
        _frames = rsiState.GetFrames(RsiDirection.South);
        _delays = rsiState.GetDelays();
        _length = _delays.Sum();
    }

    public Texture GetFrame(TimeSpan time)
    {
        if (_frames.Length == 1)
            return _frames[0];

        var t = (float) (time.TotalSeconds % _length);
        for (var i = 0; i < _frames.Length; i++)
        {
            t -= _delays[i];
            if (t < 0)
                return _frames[i];
        }

        return _frames[^1];
    }
}
