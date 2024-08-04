// using System.ComponentModel;
using System.Diagnostics;
using Content.Shared.ActionBlocker;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers;
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
using Robust.Shared.Utility;

namespace Content.Shared.SmartFridge;

public sealed class SmartFridgeSystem : EntitySystem
{
    // [Dependency] private IContainer _container = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;

    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    // [Dependency] private readonly INetManager _net = default!;
    // [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        // SubscribeLocalEvent<SmartFridgeComponent, GetVerbsEvent<InteractionVerb>>(OnInsertVerb);
        SubscribeLocalEvent<SmartFridgeComponent, ComponentInit>(OnSmartFridgeInit);
        // SubscribeLocalEvent<SmartFridgeComponent, AfterInsertEntityEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<SmartFridgeComponent, SmartFridgeDispenseItemMessage>(OnDispenseItem);

        SubscribeLocalEvent<SmartFridgeComponent, TryInsertEntityEvent>(OnTryInsertEntity);
        SubscribeLocalEvent<SmartFridgeComponent, AfterInsertEntityEvent>(OnAfterInsertEntity);
    }

    private void OnSmartFridgeInit(Entity<SmartFridgeComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Container = _containerSystem.EnsureContainer<Container>(ent, SmartFridgeComponent.ContainerId);

        UpdateUIState(ent);
    }

    private void OnTryInsertEntity(Entity<SmartFridgeComponent> ent, ref TryInsertEntityEvent args)
    {
        if (args.Handled)
        {
            return;
        }

        args.Handled = true;

        if (!TryComp<SmartFridgeCatagoryComponent>(args.Entity, out var smartFridgeCatagory))
        {
            args.Blocked = true;
            return;
        }

        // TODO: implement a check!!!
        return;
    }

    private void OnAfterInsertEntity(Entity<SmartFridgeComponent> ent, ref AfterInsertEntityEvent args)
    {
        UpdateUIState(ent);
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
        return Name(entity);
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
