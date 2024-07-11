using Content.Shared.FixedPoint;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.SmartFridge;

[RegisterComponent, NetworkedComponent]
public sealed partial class SmartFridgeComponent : Component
{
    public const string ContainerId = "smartfridge";

    public Container Container;
}

[Serializable, NetSerializable]
public sealed class SmartFridgeBountUserInterfaceState : BoundUserInterfaceState
{
    public readonly List<SmartFridgeInventoryGroup> Inventory;

    public SmartFridgeBountUserInterfaceState(List<SmartFridgeInventoryGroup> inventory)
    {
        Inventory = inventory;
    }
}

[Serializable, NetSerializable]
public sealed class SmartFridgeInventoryGroup
{
    [ViewVariables(VVAccess.ReadWrite)]
    public readonly string Name;

    [ViewVariables(VVAccess.ReadWrite)]
    public readonly List<SmartFridgeInventoryEntry> Items;

    public SmartFridgeInventoryGroup(string name, List<SmartFridgeInventoryEntry>? items = null)
    {
        Name = name;
        Items = items ?? [];
    }
}


[Serializable, NetSerializable]
public sealed class SmartFridgeInventoryEntry
{
    [ViewVariables(VVAccess.ReadWrite)]
    public string Group;

    [ViewVariables(VVAccess.ReadWrite)]
    public NetEntity VisualReference;

    [ViewVariables(VVAccess.ReadWrite)]
    public string ItemName;

    [ViewVariables(VVAccess.ReadWrite)]
    public FixedPoint2 UnitCount;

    [ViewVariables(VVAccess.ReadWrite)]
    public uint Ammount;

    public SmartFridgeInventoryEntry(string group, NetEntity visualReference, string itemName, FixedPoint2 unitCount, uint amount)
    {
        Group = group;
        VisualReference = visualReference;
        ItemName = itemName;
        UnitCount = unitCount;
        Ammount = amount;
    }
}

[Serializable, NetSerializable]
public enum SmartFridgeUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class SmartFridgeDispenseItemMessage : BoundUserInterfaceMessage
{
    public SmartFridgeInventoryEntry Item;
    public int Amount;

    public SmartFridgeDispenseItemMessage(SmartFridgeInventoryEntry item, int amount)
    {
        Item = item;
        Amount = amount;
    }
}
