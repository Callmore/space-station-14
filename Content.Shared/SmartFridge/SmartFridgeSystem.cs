// using System.ComponentModel;
using Content.Shared.ActionBlocker;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
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

    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SmartFridgeComponent, GetVerbsEvent<InteractionVerb>>(OnInsertVerb);
        SubscribeLocalEvent<SmartFridgeComponent, ComponentInit>(OnSmartFridgeInit);
        SubscribeLocalEvent<SmartFridgeComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
    }

    private void OnSmartFridgeInit(EntityUid uid, SmartFridgeComponent component, ComponentInit args)
    {
        component.Container = _containerSystem.EnsureContainer<Container>(uid, SmartFridgeComponent.ContainerId);
    }

    private void OnAfterInteractUsing(EntityUid uid, SmartFridgeComponent component, AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (!HasComp<HandsComponent>(args.User))
        {
            return;
        }

        if (!CanInsert(uid, component, args.Used) || !_handsSystem.TryDropIntoContainer(args.User, args.Used, component.Container))
        {
            return;
        }

        // _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(args.User):player} inserted {ToPrettyString(args.Used)} into {ToPrettyString(uid)}");
        AfterInsert(uid, component, args.Used, args.User);
        args.Handled = true;
    }

    private void OnInsertVerb(EntityUid uid, SmartFridgeComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null || args.Using == null)
            return;

        if (!_actionBlockerSystem.CanDrop(args.User))
            return;

        if (!CanInsert(uid, component, args.Using.Value))
            return;

        var insertVerb = new InteractionVerb()
        {
            Text = Name(args.Using.Value),
            Category = VerbCategory.Insert,
            Act = () =>
            {
                _handsSystem.TryDropIntoContainer(args.User, args.Using.Value, component.Container, checkActionBlocker: false, handsComp: args.Hands);
                // TODO: Admin logging? after insert (yes smartfridges are just disposal bins)
                AfterInsert(uid, component, args.Using.Value, args.User);
            }
        };

        args.Verbs.Add(insertVerb);
    }

    public bool CanInsert(EntityUid uid, SmartFridgeComponent component, EntityUid entity)
    {
        if (!Transform(uid).Anchored)
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

    public void AfterInsert(EntityUid uid, SmartFridgeComponent component, EntityUid inserted, EntityUid? user = null, bool doInsert = false)
    {
        // _audioSystem.PlayPvs(component.InsertSound, uid);

        if (doInsert && !_containerSystem.Insert(inserted, component.Container))
            return;

        if (_net.IsServer)
        {
            _ui.ServerSendUiMessage(uid, SmartFridgeUiKey.Key, new SmartFridgeUpdateInventoryMessage());
        }

        // if (user != inserted && user != null)
        //     _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user.Value):player} inserted {ToPrettyString(inserted)} into {ToPrettyString(uid)}");

        // QueueAutomaticEngage(uid, component);

        // _ui.CloseUi(uid, SharedDisposalUnitComponent.DisposalUnitUiKey.Key, inserted);

        // // Maybe do pullable instead? Eh still fine.
        // Joints.RecursiveClearJoints(inserted);
        // UpdateVisualState(uid, component);
    }

    public List<SmartFridgeInventoryGroup> GetSortedInventory(EntityUid uid, SmartFridgeComponent? component = null)
    {
        if (!Resolve(uid, ref component))
        {
            return [];
        }

        var groups = new Dictionary<string, List<SmartFridgeInventoryEntry>>();

        var container = _containerSystem.GetContainer(uid, SmartFridgeComponent.ContainerId);
        foreach (var entity in container.ContainedEntities)
        {
            if (!TryComp<SmartFridgeCatagoryComponent>(entity, out var catagory))
            {
                continue;
            }

            if (!groups.TryGetValue(catagory.Catagory, out var group))
            {
                group = [];
                groups[catagory.Catagory] = group;
            }
            var a = GetNetEntity(entity);
            // TODO: combine entries
            group.Add(new SmartFridgeInventoryEntry(a, 1));
        }

        var result = new List<SmartFridgeInventoryGroup>();
        foreach (var (catagory, entities) in groups)
        {
            result.Add(new(catagory, entities));
        }

        return result;
    }
}
