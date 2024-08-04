
using Robust.Shared.Containers;

namespace Content.Shared.Containers;

[RegisterComponent]
public sealed partial class ClickToInsertComponent : Component
{
    [DataField]
    public string Target = string.Empty;

    public Container Container;
}
