using Content.Shared._Floof.Arrivals;
using Content.Shared.Camera;
using Robust.Client.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Client._Floof.Arrivals;

public sealed partial class ArrivalsLandingShakeSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;

    private const int Shakes = 10;
    private const float Strength = 0.6f;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(0.15);

    [SubscribeNetworkEvent]
    private void OnLanded(ArrivalsLandedEvent ev)
    {
        for (var i = 0; i < Shakes; i++)
        {
            var strength = Strength * (Shakes - i) / Shakes;
            Timer.Spawn(Interval * i, () => Shake(strength));
        }
    }

    private void Shake(float strength)
    {
        if (_player.LocalEntity is { } player)
            _recoil.KickCamera(player, _random.NextAngle().ToVec() * strength);
    }
}
