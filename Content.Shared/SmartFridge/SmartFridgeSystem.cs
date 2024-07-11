// using System.ComponentModel;
using System.Diagnostics;
using Content.Shared.ActionBlocker;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Labels.Components;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;

namespace Content.Shared.SmartFridge;

public sealed class SmartFridgeSystem : EntitySystem
{
    // [Dependency] private IContainer _container = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;

    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmartFridgeComponent, GetVerbsEvent<InteractionVerb>>(OnInsertVerb);
        SubscribeLocalEvent<SmartFridgeComponent, ComponentInit>(OnSmartFridgeInit);
        SubscribeLocalEvent<SmartFridgeComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<SmartFridgeComponent, SmartFridgeDispenseItemMessage>(OnDispenseItem);
    }



    private void OnSmartFridgeInit(Entity<SmartFridgeComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Container = _containerSystem.EnsureContainer<Container>(ent, SmartFridgeComponent.ContainerId);

        UpdateUIState(ent);
    }

    private void OnAfterInteractUsing(Entity<SmartFridgeComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (!HasComp<HandsComponent>(args.User))
        {
            return;
        }

        if (!CanInsert(ent, args.Used) || !_handsSystem.TryDropIntoContainer(args.User, args.Used, ent.Comp.Container))
        {
            return;
        }

        // _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(args.User):player} inserted {ToPrettyString(args.Used)} into {ToPrettyString(uid)}");
        AfterInsert(ent, args.Used, args.User);
        args.Handled = true;
    }

    private void OnInsertVerb(Entity<SmartFridgeComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || args.Using == null)
            return;

        if (!_actionBlockerSystem.CanDrop(args.User))
            return;

        if (!CanInsert(ent, args.Using.Value))
            return;

        var user = args.User;
        var heldObject = args.Using.Value;
        var hands = args.Hands;

        var insertVerb = new InteractionVerb()
        {
            Text = Name(args.Using.Value),
            Category = VerbCategory.Insert,
            Act = () =>
            {
                _handsSystem.TryDropIntoContainer(user, heldObject, ent.Comp.Container, checkActionBlocker: false, handsComp: hands);
                // TODO: Admin logging? after insert (yes smartfridges are just disposal bins)
                AfterInsert(ent, heldObject, user);
            }
        };

        args.Verbs.Add(insertVerb);
    }

    public bool CanInsert(Entity<SmartFridgeComponent> ent, EntityUid entity)
    {
        if (!Transform(ent).Anchored)
            return false;

        var storable = HasComp<ItemComponent>(entity);

        // if (_whitelistSystem.IsBlacklistPass(component.Blacklist, entity) ||
        //     _whitelistSystem.IsWhitelistFail(component.Whitelist, entity))
        //     return false;

        if (TryComp<PhysicsComponent>(entity, out var physics) && physics.CanCollide || storable)
        {
            return true;
        }
        return false;
    }

    public void AfterInsert(Entity<SmartFridgeComponent> ent, EntityUid inserted, EntityUid? user = null, bool doInsert = false)
    {
        // _audioSystem.PlayPvs(component.InsertSound, uid);

        if (doInsert && !_containerSystem.Insert(inserted, ent.Comp.Container))
            return;

        UpdateUIState(ent);

        // if (user != inserted && user != null)
        //     _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user.Value):player} inserted {ToPrettyString(inserted)} into {ToPrettyString(uid)}");

        // QueueAutomaticEngage(uid, component);

        // _ui.CloseUi(uid, SharedDisposalUnitComponent.DisposalUnitUiKey.Key, inserted);

        // // Maybe do pullable instead? Eh still fine.
        // Joints.RecursiveClearJoints(inserted);
        // UpdateVisualState(uid, component);
    }

    private void OnDispenseItem(Entity<SmartFridgeComponent> ent, ref SmartFridgeDispenseItemMessage args)
    {
        // throw new NotImplementedException();
        // Find item
        // Log.Debug(ToPrettyString(args.Actor));

        var itemsToRemove = new List<EntityUid>();
        var toGrab = args.Amount;

        Log.Debug($"Trying to get {toGrab} thing(s) from {ToPrettyString(ent)}.");

        var container = _containerSystem.GetContainer(ent, SmartFridgeComponent.ContainerId);
        foreach (var entity in container.ContainedEntities)
        {
            Log.Debug($"Trying {ToPrettyString(entity)}...");

            var catagory = "unknown";
            if (TryComp<SmartFridgeCatagoryComponent>(entity, out var smartFridgeCatagoryComponent))
            {
                catagory = smartFridgeCatagoryComponent.Catagory;
            }

            Log.Debug($"Checking group... ({catagory} == {args.Item.Group})");
            if (catagory != args.Item.Group)
            {
                Log.Debug("Did not match, continuing...");
                continue;
            }

            Log.Debug($"Checking name... ({GetNameForEntity(entity)} == {args.Item.ItemName})");
            if (GetNameForEntity(entity) != args.Item.ItemName)
            {
                Log.Debug("Did not match, continuing...");
                continue;
            }

            Log.Debug($"Checking unit count... ({GetUnitCountOfEntity(entity)} == {args.Item.UnitCount})");
            if (GetUnitCountOfEntity(entity) != args.Item.UnitCount)
            {
                Log.Debug("Did not match, continuing...");
                continue;
            }

            itemsToRemove.Add(entity);

            if (toGrab > 0 && itemsToRemove.Count >= toGrab)
            {
                break;
            }

        }

        foreach (var entity in itemsToRemove)
        {
            Log.Debug("Attempting to pick up item...");
            if (!_handsSystem.TryPickupAnyHand(args.Actor, entity))
            {
                Log.Debug("Failed, Attempting to drop on floor...");
                if (!_containerSystem.Remove(entity, container))
                {
                    Log.Warning("what");
                }
            }
        }

        UpdateUIState(ent);
    }
    public void UpdateUIState(Entity<SmartFridgeComponent> ent)
    {
        _ui.SetUiState(ent.Owner, SmartFridgeUiKey.Key, new SmartFridgeBountUserInterfaceState(GetSortedInventory(ent.Owner)));
    }

    public List<SmartFridgeInventoryGroup> GetSortedInventory(Entity<SmartFridgeComponent?> ent)
    {
        if (!Resolve(ent.Owner, ref ent.Comp))
        {
            return [];
        }

        var groups = new Dictionary<string, List<SmartFridgeInventoryEntry>>();

        var container = _containerSystem.GetContainer(ent, SmartFridgeComponent.ContainerId);
        foreach (var entity in container.ContainedEntities)
        {
            if (!TryComp<SmartFridgeCatagoryComponent>(entity, out var catagory))
            {
                continue;
            }

            var name = GetNameForEntity(entity);

            if (name == null)
            {
                continue;
            }

            var unitCount = GetUnitCountOfEntity(entity);

            if (!groups.TryGetValue(catagory.Catagory, out var group))
            {
                group = [];
                groups[catagory.Catagory] = group;
            }

            // Check we already have the value
            var shouldContinue = false;
            foreach (var item in group)
            {
                if (item.ItemName == name && item.UnitCount == unitCount)
                {
                    item.Ammount++;
                    shouldContinue = true;
                    break;
                }
            }
            if (shouldContinue)
            {
                continue;
            }

            Log.Debug(name);

            // TODO: combine entries
            group.Add(new SmartFridgeInventoryEntry(catagory.Catagory, GetNetEntity(entity), name, unitCount, 1));
        }

        var result = new List<SmartFridgeInventoryGroup>();
        foreach (var (catagory, entities) in groups)
        {
            result.Add(new(catagory, entities));
        }

        return result;
    }

    private string? GetNameForEntity(EntityUid entity)
    {
        if (TryComp<LabelComponent>(entity, out var labelComponent))
        {
            return labelComponent.CurrentLabel;
        }
        else if (TryComp<MetaDataComponent>(entity, out var metaDataComponent))
        {
            return metaDataComponent.EntityName;
        }
        return null;
    }


    public FixedPoint2 GetUnitCountOfEntity(Entity<SolutionContainerManagerComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp))
        {
            return 0;
        }

        var unitCount = FixedPoint2.New(-1);
        if (TryComp<SolutionContainerManagerComponent>(ent, out var solutionContainerManagerComponent))
        {
            unitCount = 0;
            foreach (var (_, solution) in _solutionContainerSystem.EnumerateSolutions((ent, solutionContainerManagerComponent)))
            {
                unitCount += solution.Comp.Solution.Volume;
            }
        }

        return unitCount;
    }
}
