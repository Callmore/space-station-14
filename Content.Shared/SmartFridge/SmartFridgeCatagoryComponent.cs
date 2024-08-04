using Robust.Shared.GameStates;

namespace Content.Shared.SmartFridge;

[RegisterComponent, NetworkedComponent]
public sealed partial class SmartFridgeCatagoryComponent : Component
{
    [DataField]
    public string Catagory = string.Empty;
}
