
using Content.Client.UserInterface.Controls;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client.SmartFridge.UI;

[GenerateTypedNameReferences]
public sealed partial class SmartFridgeMenuItem : PanelContainer
{
    public SmartFridgeMenuItem()
    {
        RobustXamlLoader.Load(this);
        IoCManager.InjectDependencies(this);

        // foreach (var item in SmartFridgeItems)
        // {

        // }
    }
}
