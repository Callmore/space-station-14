
using Content.Shared.ActionBlocker;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;

namespace Content.Shared.Containers;

public sealed partial class ClickToInsertSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClickToInsertComponent, ComponentInit>(OnClickToInsertInit);
        SubscribeLocalEvent<ClickToInsertComponent, GetVerbsEvent<InteractionVerb>>(OnInsertVerb);
        SubscribeLocalEvent<ClickToInsertComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
    }

    private void OnClickToInsertInit(Entity<ClickToInsertComponent> ent, ref ComponentInit args)
    {
        Log.Debug($"Initialising container '{ent.Comp.Target}'");
        ent.Comp.Container = _containerSystem.EnsureContainer<Container>(ent, ent.Comp.Target);
    }

    private void OnInsertVerb(Entity<ClickToInsertComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
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

    private void OnAfterInteractUsing(Entity<ClickToInsertComponent> ent, ref AfterInteractUsingEvent args)
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


    private bool CanInsert(Entity<ClickToInsertComponent> ent, EntityUid entity)
    {
        if (!Transform(ent).Anchored)
            return false;

        var storable = HasComp<ItemComponent>(entity);

        // if (_whitelistSystem.IsBlacklistPass(component.Blacklist, entity) ||
        //     _whitelistSystem.IsWhitelistFail(component.Whitelist, entity))
        //     return false;

        if (TryComp<PhysicsComponent>(entity, out var physics) && physics.CanCollide || storable)
        {
            var ev = new TryInsertEntityEvent
            {
                Entity = entity
            };
            RaiseLocalEvent(ent, ref ev);

            return !ev.Blocked;
        }
        return false;
    }

    private void AfterInsert(Entity<ClickToInsertComponent> ent, EntityUid inserted, EntityUid? user = null, bool doInsert = false)
    {
        // _audioSystem.PlayPvs(component.InsertSound, uid);

        if (doInsert && !_containerSystem.Insert(inserted, ent.Comp.Container))
            return;

        var ev = new AfterInsertEntityEvent
        {
            Entity = inserted,
            User = user,
        };
        RaiseLocalEvent(ent, ref ev);

        // UpdateUIState(ent);

        // if (user != inserted && user != null)
        //     _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user.Value):player} inserted {ToPrettyString(inserted)} into {ToPrettyString(uid)}");

        // QueueAutomaticEngage(uid, component);

        // _ui.CloseUi(uid, SharedDisposalUnitComponent.DisposalUnitUiKey.Key, inserted);

        // // Maybe do pullable instead? Eh still fine.
        // Joints.RecursiveClearJoints(inserted);
        // UpdateVisualState(uid, component);
    }

}

[ByRefEvent] public record struct TryInsertEntityEvent(EntityUid Entity, bool Blocked = false, bool Handled = false);
[ByRefEvent] public record struct AfterInsertEntityEvent(EntityUid Entity, EntityUid? User = null);
