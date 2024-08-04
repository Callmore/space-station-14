using System.Diagnostics.CodeAnalysis;
using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Containers;
using Content.Shared.Database;
using Content.Shared.Disposal.Components;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Emag.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Disposal;

[Serializable, NetSerializable]
public sealed partial class DisposalDoAfterEvent : SimpleDoAfterEvent
{
}

public abstract class SharedDisposalUnitSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming GameTiming = default!;
    [Dependency] protected readonly MetaDataSystem Metadata = default!;
    [Dependency] protected readonly SharedJointSystem Joints = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] protected readonly SharedPopupSystem PopupSystem = default!;
    [Dependency] protected readonly SharedDoAfterSystem DoAfterSystem = default!;
    [Dependency] protected readonly SharedAudioSystem AudioSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    protected static TimeSpan ExitAttemptDelay = TimeSpan.FromSeconds(0.5);

    // Percentage
    public const float PressurePerSecond = 0.05f;

    public abstract bool HasDisposals([NotNullWhen(true)] EntityUid? uid);

    public abstract bool ResolveDisposals(EntityUid uid, [NotNullWhen(true)] ref SharedDisposalUnitComponent? component);

    /// <summary>
    /// Gets the current pressure state of a disposals unit.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    /// <param name="metadata"></param>
    /// <returns></returns>
    public DisposalsPressureState GetState(EntityUid uid, SharedDisposalUnitComponent component, MetaDataComponent? metadata = null)
    {
        var nextPressure = Metadata.GetPauseTime(uid, metadata) + component.NextPressurized - GameTiming.CurTime;
        var pressurizeTime = 1f / PressurePerSecond;
        var pressurizeDuration = pressurizeTime - component.FlushDelay.TotalSeconds;

        if (nextPressure.TotalSeconds > pressurizeDuration)
        {
            return DisposalsPressureState.Flushed;
        }

        if (nextPressure > TimeSpan.Zero)
        {
            return DisposalsPressureState.Pressurizing;
        }

        return DisposalsPressureState.Ready;
    }

    public float GetPressure(EntityUid uid, SharedDisposalUnitComponent component, MetaDataComponent? metadata = null)
    {
        if (!Resolve(uid, ref metadata))
            return 0f;

        var pauseTime = Metadata.GetPauseTime(uid, metadata);
        return MathF.Min(1f,
            (float) (GameTiming.CurTime - pauseTime - component.NextPressurized).TotalSeconds / PressurePerSecond);
    }

    protected void OnPreventCollide(EntityUid uid, SharedDisposalUnitComponent component,
        ref PreventCollideEvent args)
    {
        var otherBody = args.OtherEntity;

        // Items dropped shouldn't collide but items thrown should
        if (HasComp<ItemComponent>(otherBody) && !HasComp<ThrownItemComponent>(otherBody))
        {
            args.Cancelled = true;
            return;
        }

        if (component.RecentlyEjected.Contains(otherBody))
        {
            args.Cancelled = true;
        }
    }

    protected void OnCanDragDropOn(EntityUid uid, SharedDisposalUnitComponent component, ref CanDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.CanDrop = CanInsert(uid, component, args.Dragged);
        args.Handled = true;
    }

    protected void OnEmagged(EntityUid uid, SharedDisposalUnitComponent component, ref GotEmaggedEvent args)
    {
        component.DisablePressure = true;
        args.Handled = true;
    }

    public virtual bool CanInsert(EntityUid uid, SharedDisposalUnitComponent component, EntityUid entity)
    {
        if (!Transform(uid).Anchored)
            return false;

        var storable = HasComp<ItemComponent>(entity);
        if (!storable && !HasComp<BodyComponent>(entity))
            return false;

        if (_whitelistSystem.IsBlacklistPass(component.Blacklist, entity) ||
            _whitelistSystem.IsWhitelistFail(component.Whitelist, entity))
            return false;

        if (TryComp<PhysicsComponent>(entity, out var physics) && (physics.CanCollide) || storable)
            return true;
        else
            return false;

    }

    public abstract void DoInsertDisposalUnit(EntityUid uid, EntityUid toInsert, EntityUid user, SharedDisposalUnitComponent? disposal = null);

    [Serializable, NetSerializable]
    protected sealed class DisposalUnitComponentState : ComponentState
    {
        public SoundSpecifier? FlushSound;
        public DisposalsPressureState State;
        public TimeSpan NextPressurized;
        public TimeSpan AutomaticEngageTime;
        public TimeSpan? NextFlush;
        public bool Powered;
        public bool Engaged;
        public List<NetEntity> RecentlyEjected;

        public DisposalUnitComponentState(SoundSpecifier? flushSound, DisposalsPressureState state, TimeSpan nextPressurized, TimeSpan automaticEngageTime, TimeSpan? nextFlush, bool powered, bool engaged, List<NetEntity> recentlyEjected)
        {
            FlushSound = flushSound;
            State = state;
            NextPressurized = nextPressurized;
            AutomaticEngageTime = automaticEngageTime;
            NextFlush = nextFlush;
            Powered = powered;
            Engaged = engaged;
            RecentlyEjected = recentlyEjected;
        }
    }

    public bool TryInsert(EntityUid unitId, EntityUid toInsertId, EntityUid? userId, SharedDisposalUnitComponent? unit = null)
    {
        if (!Resolve(unitId, ref unit))
            return false;

        if (userId.HasValue && !HasComp<HandsComponent>(userId) && toInsertId != userId) // Mobs like mouse can Jump inside even with no hands
        {
            PopupSystem.PopupEntity(Loc.GetString("disposal-unit-no-hands"), userId.Value, userId.Value, PopupType.SmallCaution);
            return false;
        }

        if (!CanInsert(unitId, unit, toInsertId))
            return false;

        bool insertingSelf = userId == toInsertId;

        var delay = insertingSelf ? unit.EntryDelay : unit.DraggedEntryDelay;

        if (userId != null && !insertingSelf)
            PopupSystem.PopupEntity(Loc.GetString("disposal-unit-being-inserted", ("user", Identity.Entity((EntityUid)userId, EntityManager))), toInsertId, toInsertId, PopupType.Large);

        if (delay <= 0 || userId == null)
        {
            AfterInsert(unitId, unit, toInsertId, userId, doInsert: true);
            return true;
        }

        // Can't check if our target AND disposals moves currently so we'll just check target.
        // if you really want to check if disposals moves then add a predicate.
        var doAfterArgs = new DoAfterArgs(EntityManager, userId.Value, delay, new DisposalDoAfterEvent(), unitId, target: toInsertId, used: unitId)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false
        };

        DoAfterSystem.TryStartDoAfter(doAfterArgs);
        return true;
    }

    public void AfterInsert(EntityUid uid, SharedDisposalUnitComponent component, EntityUid inserted, EntityUid? user = null, bool doInsert = false)
    {
        AudioSystem.PlayPvs(component.InsertSound, uid);

        if (doInsert && !_containerSystem.Insert(inserted, component.Container))
            return;

        if (user != inserted && user != null)
            _adminLogger.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user.Value):player} inserted {ToPrettyString(inserted)} into {ToPrettyString(uid)}");

        QueueAutomaticEngage(uid, component);

        _ui.CloseUi(uid, SharedDisposalUnitComponent.DisposalUnitUiKey.Key, inserted);

        // Maybe do pullable instead? Eh still fine.
        Joints.RecursiveClearJoints(inserted);
        UpdateVisualState(uid, component);
    }

    /// <summary>
    /// If something is inserted (or the likes) then we'll queue up an automatic flush in the future.
    /// </summary>
    public void QueueAutomaticEngage(EntityUid uid, SharedDisposalUnitComponent component, MetaDataComponent? metadata = null)
    {
        if (component.Deleted || !component.AutomaticEngage || !component.Powered && component.Container.ContainedEntities.Count == 0)
        {
            return;
        }

        var pauseTime = Metadata.GetPauseTime(uid, metadata);
        var automaticTime = GameTiming.CurTime + component.AutomaticEngageTime - pauseTime;
        var flushTime = TimeSpan.FromSeconds(Math.Min((component.NextFlush ?? TimeSpan.MaxValue).TotalSeconds, automaticTime.TotalSeconds));

        component.NextFlush = flushTime;
        Dirty(uid, component);
    }

    public void UpdateVisualState(EntityUid uid, SharedDisposalUnitComponent component, bool flush = false)
    {
        if (!TryComp(uid, out AppearanceComponent? appearance))
        {
            return;
        }

        if (!Transform(uid).Anchored)
        {
            _appearance.SetData(uid, SharedDisposalUnitComponent.Visuals.VisualState, SharedDisposalUnitComponent.VisualState.UnAnchored, appearance);
            _appearance.SetData(uid, SharedDisposalUnitComponent.Visuals.Handle, SharedDisposalUnitComponent.HandleState.Normal, appearance);
            _appearance.SetData(uid, SharedDisposalUnitComponent.Visuals.Light, SharedDisposalUnitComponent.LightStates.Off, appearance);
            return;
        }

        var state = GetState(uid, component);

        switch (state)
        {
            case DisposalsPressureState.Flushed:
                _appearance.SetData(uid, SharedDisposalUnitComponent.Visuals.VisualState, SharedDisposalUnitComponent.VisualState.OverlayFlushing, appearance);
                break;
            case DisposalsPressureState.Pressurizing:
                _appearance.SetData(uid, SharedDisposalUnitComponent.Visuals.VisualState, SharedDisposalUnitComponent.VisualState.OverlayCharging, appearance);
                break;
            case DisposalsPressureState.Ready:
                _appearance.SetData(uid, SharedDisposalUnitComponent.Visuals.VisualState, SharedDisposalUnitComponent.VisualState.Anchored, appearance);
                break;
        }

        _appearance.SetData(uid, SharedDisposalUnitComponent.Visuals.Handle, component.Engaged
            ? SharedDisposalUnitComponent.HandleState.Engaged
            : SharedDisposalUnitComponent.HandleState.Normal, appearance);

        if (!component.Powered)
        {
            _appearance.SetData(uid, SharedDisposalUnitComponent.Visuals.Light, SharedDisposalUnitComponent.LightStates.Off, appearance);
            return;
        }

        var lightState = SharedDisposalUnitComponent.LightStates.Off;

        if (component.Container.ContainedEntities.Count > 0)
        {
            lightState |= SharedDisposalUnitComponent.LightStates.Full;
        }

        if (state is DisposalsPressureState.Pressurizing or DisposalsPressureState.Flushed)
        {
            lightState |= SharedDisposalUnitComponent.LightStates.Charging;
        }
        else
        {
            lightState |= SharedDisposalUnitComponent.LightStates.Ready;
        }

        _appearance.SetData(uid, SharedDisposalUnitComponent.Visuals.Light, lightState, appearance);
    }


    protected void OnTryInsertEntity(Entity<SharedDisposalUnitComponent> ent, ref TryInsertEntityEvent args)
    {
        args.Blocked = !CanInsert(ent, ent, args.Entity);
        args.Handled = true;
    }

    protected void OnAfterInsertEntity(Entity<SharedDisposalUnitComponent> ent, ref AfterInsertEntityEvent args)
    {
        AfterInsert(ent, ent, args.Entity, args.User);
    }
}
