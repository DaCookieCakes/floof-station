using System.Linq;
using Content.Shared.Disposal.Holder;
using Content.Shared.Disposal.Unit;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Starlight
{
    public sealed partial class AutoLoaderSystem : EntitySystem
    {
        [Dependency] private SharedDisposalHolderSystem _disposalHolder = default!;
        [Dependency] private SharedContainerSystem _containerSystem = default!;
        [Dependency] private SharedTransformSystem _xformSystem = default!;
        [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
        }

        public void Cycle(EntityUid entity, Entity<AutoLoaderComponent> autoloader, BaseContainer autoloadercontainer, EntityUid CurrentTube)
        {
            var holder = Spawn(autoloader.Comp.HolderPrototypeId, _xformSystem.GetMapCoordinates(autoloader, xform: Transform(autoloader)));
            var holderComponent = Comp<DisposalHolderComponent>(holder);
            if (holderComponent.Container is not { } holderContainer)
                return;

            foreach (var item in autoloadercontainer.ContainedEntities.ToArray())
                if (entity != item)
                    _containerSystem.Insert(item, holderContainer);

            if (_whitelistSystem.IsWhitelistPass(autoloader.Comp.Whitelist, entity))
                _containerSystem.Insert(entity, autoloadercontainer);
            else
                _containerSystem.Insert(entity, holderContainer);

            _disposalHolder.TryEnterTube((holder, holderComponent), CurrentTube);
        }
    }

    [RegisterComponent]
    public sealed partial class AutoLoaderComponent : Component
    {
        [DataField(required: true)]
        public string Container;

        [DataField]
        public EntityWhitelist? Whitelist;

        [DataField]
        public EntProtoId HolderPrototypeId = "DisposalHolder";
    }
}

