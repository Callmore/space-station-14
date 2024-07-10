using Robust.Shared.GameStates;

namespace Content.Shared.SmartFridge;

[RegisterComponent, NetworkedComponent]
public sealed partial class SmartFridgeCatagoryComponent : Component
{
    [DataField("catagory", required: true)]
    public string Catagory = string.Empty;
}
