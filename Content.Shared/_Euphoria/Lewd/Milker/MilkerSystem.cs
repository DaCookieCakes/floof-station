using System.Numerics;
using Content.Shared._Euphoria.Lewd.Milker.Components;
using Content.Shared._Floof.Leash.Components;
using Content.Shared._Floof.Sex.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Audio;
using Content.Shared.Body;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Dataset;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Euphoria.Lewd.Milker;

[Serializable]
public sealed partial class MilkerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popupSystem = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private BodySystem _body = default!; // Floof
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MilkerComponent, ComponentStartup>(OnComponentInit);
        SubscribeLocalEvent<MilkerComponent, CanDropTargetEvent>(OnCanDrop);
        SubscribeLocalEvent<MilkerComponent, DragDropTargetEvent>(OnDragDropped);
        SubscribeLocalEvent<MilkerComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<MilkerComponent, MilkerDoAfterEvent>(FinishAttaching);
        SubscribeLocalEvent<MilkerComponent, DestructionEventArgs>(OnDestroyed);
    }

    private void OnComponentInit(Entity<MilkerComponent> entity, ref ComponentStartup args)
    {
        base.Initialize();
    }

    private void OnDestroyed(Entity<MilkerComponent> entity, ref DestructionEventArgs args)
    {
        Detach(entity);
    }

    private void OnCanDrop(Entity<MilkerComponent> entity, ref CanDropTargetEvent args)
    {
        args.Handled = true;
        args.CanDrop |= CheckInteraction(entity, args.Dragged, args.User);
    }

    private void OnDragDropped(Entity<MilkerComponent> entity, ref DragDropTargetEvent args)
    {
        args.Handled = true;
        TryAttaching(entity, args.Dragged, args.User);

    }

    private void OnInteractHand(Entity<MilkerComponent> entity, ref InteractHandEvent args)
    {
        TryAttaching(entity, args.User, args.User);
    }

    public void TryAttaching(Entity<MilkerComponent> entity, EntityUid target, EntityUid user)
    {
        if (!CheckInteraction(entity, target, user))
            return;

        if (entity.Comp.MilkedEntity != null)
        {
            Detach(entity);
        }
        else if (CheckMilkable(entity, target))
        {
            var doAfterArgs = new DoAfterArgs(EntityManager, user, entity.Comp.AttachDelay, new MilkerDoAfterEvent(), entity, target)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                AttemptFrequency = AttemptFrequency.EveryTick
            };
            _popupSystem.PopupEntity(Loc.GetString("milker-attach-start-popup",
                ("user", Identity.Entity(user, EntityManager)),
                ("target", Identity.Entity(target, EntityManager)),
                ("entity", Identity.Entity(entity, EntityManager))
            ), entity.Owner, user);
            _doAfter.TryStartDoAfter(doAfterArgs);
        }
        else
        {
            // Only shown to the person who attempted to attach them
            _popupSystem.PopupEntity(Loc.GetString("milker-attach-fail-popup",
                ("target", Identity.Entity(target, EntityManager))
            ), entity.Owner, user);
        }
    }

    void FinishAttaching(Entity<MilkerComponent> entity, ref MilkerDoAfterEvent args)
    {
        if (!args.Cancelled && args.Target != null)
        {
            Attach(entity, (EntityUid)args.Target);
        }
    }

    public void Attach(Entity<MilkerComponent> entity, EntityUid target)
    {
        if (_net.IsClient)
            return;

        _popupSystem.PopupEntity(Loc.GetString("milker-attach-finish-popup",
            ("target", Identity.Entity(target, EntityManager)),
            ("entity", Identity.Entity(entity, EntityManager))
        ), entity.Owner, target);
        _ambient.SetAmbience(entity, true);

        // Floof - solutions may live on an organ inside the body rather than the mob itself
        if (!TryGetMilkSource(entity, target, out var sourceEntity, out var sourceSolution))
            return;
        entity.Comp.MilkedSolutionEntity = sourceEntity;
        entity.Comp.MilkedSolution = sourceSolution;
        entity.Comp.MilkedEntity = target;

        if (entity.Comp.TubeSprite is { } sprite)
        {
            _container.EnsureContainer<ContainerSlot>(entity, MilkerComponent.VisualsContainerName);
            if (TrySpawnInContainer(null, entity, MilkerComponent.VisualsContainerName, out var visualEntity))
            {
                var visualComp = EnsureComp<LeashedVisualsComponent>(visualEntity.Value);
                visualComp.Sprite = sprite;
                visualComp.Source = entity;
                visualComp.Target = target;
            }
        }
        Dirty(entity);
    }

    public void Detach(Entity<MilkerComponent> entity)
    {
        if (_net.IsClient)
            return;

        if (entity.Comp.MilkedEntity != null)
        {
            if (Exists(entity.Comp.MilkedEntity))
            {
                _popupSystem.PopupEntity(Loc.GetString("milker-detach-popup",
                    ("target", Identity.Entity((EntityUid)entity.Comp.MilkedEntity, EntityManager)),
                    ("entity", Identity.Entity(entity, EntityManager))
                ), entity.Owner, entity.Comp.MilkedEntity);
            }
            _ambient.SetAmbience(entity, false);
            entity.Comp.MilkedSolution = null;
            entity.Comp.MilkedSolutionEntity = null; // Floof
            entity.Comp.MilkedEntity = null;
            if (_container.TryGetContainer(entity, MilkerComponent.VisualsContainerName, out var visualsContainer))
                _container.CleanContainer(visualsContainer);
            Dirty(entity);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<MilkerComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            // If not attached to anything, skip
            if (component.MilkedEntity == null)
                continue;

            // Check if it's time for our next tick
            if (_timing.CurTime < component.NextUpdate)
                continue;
            component.NextUpdate = _timing.CurTime + component.UpdateDelay;

            // If the milked entity no longer exists, detach
            if (!Exists(component.MilkedEntity))
            {
                Detach(new Entity<MilkerComponent>(uid, component));
                continue;
            }

            // If the milked entity is too far away, detach them
            if (Vector2.DistanceSquared(
                _xform.GetWorldPosition(uid),
                _xform.GetWorldPosition((EntityUid)component.MilkedEntity))
                > component.TubeLength * component.TubeLength)
            {
                Detach(new Entity<MilkerComponent>(uid, component));
                continue;
            }

            // Check if we have power (also should check if the power switch is on)
            if (!_powerReceiver.IsPowered(uid))
                continue;

            if (component.MilkedSolution == null || component.MilkedSolutionEntity is not { } sourceEntity) // Floof
                continue;

            // Do the milking
            _solution.TryGetSolution(uid, component.Solution, out Entity<SolutionComponent>? solution);
            _solution.TryGetSolution(sourceEntity, component.MilkedSolution, out Entity<SolutionComponent>? source);
            if (solution == null || source == null)
                continue;

            _solution.TryTransferSolution(
                (Entity<SolutionComponent>)solution,
                ((SolutionComponent)source).Solution,
                component.MilkedAmount);

            // Display some supportive prompts
            _prototypeManager.TryIndex(component.PopupDataset, out LocalizedDatasetPrototype? dataset);
            if (dataset != null && _random.NextFloat() < component.PopupChance)
                _popupSystem.PopupEntity(Loc.GetString(_random.Pick(dataset.Values)), (EntityUid)component.MilkedEntity, (EntityUid)component.MilkedEntity, PopupType.Medium);
        }
    }

    // Checks if the entity is whitelist valid - can we milk this twink?
    public bool CheckMilkable(Entity<MilkerComponent> milker, EntityUid target)
    {
        if (!_whitelist.IsWhitelistPass(milker.Comp.MilkedEntityWhitelist, target))
            return false;
        return TryGetMilkSource(milker, target, out _, out _); // Floof
    }

    // Floof - finds the entity and solution to milk from.
    // Producer genitals are checked first.
    public bool TryGetMilkSource(Entity<MilkerComponent> milker, EntityUid target, out EntityUid source, out string solutionName)
    {
        foreach (var organ in _body.EnumerateOrgans<ProducerGenitalComponent>(target))
        {
            var id = organ.Comp2.SolutionId;
            if (!milker.Comp.MilkedSolutionWhitelist.Contains(id)
                || !_solution.TryGetSolution(organ.Owner, id, out _, out _, false))
                continue;

            source = organ.Owner;
            solutionName = id;
            return true;
        }

        foreach (var (name, _) in _solution.EnumerateSolutions(target))
        {
            if (name == null || !milker.Comp.MilkedSolutionWhitelist.Contains(name))
                continue;

            source = target;
            solutionName = name;
            return true;
        }

        source = default;
        solutionName = string.Empty;
        return false;
    }

    // Checks if the entity can actually be reached by the milker and milkee
    public bool CheckInteraction(Entity<MilkerComponent> milker, EntityUid target, EntityUid user)
    {
        if (!_actionBlocker.CanComplexInteract(user))
            return false;

        if (!_interaction.InRangeUnobstructed(user, target))
            return false;
        return true;
    }

}
