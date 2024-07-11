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
    public NetEntity Entity;


    [ViewVariables(VVAccess.ReadWrite)]
    public uint Ammount;

    public SmartFridgeInventoryEntry(NetEntity entity, uint amount)
    {
        Entity = entity;
        Ammount = amount;
    }
}

[Serializable, NetSerializable]
public enum SmartFridgeUiKey
{
    Key,
}
