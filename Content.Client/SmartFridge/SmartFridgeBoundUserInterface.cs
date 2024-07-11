
using Content.Client.SmartFridge.UI;
using Content.Shared.SmartFridge;

namespace Content.Client.SmartFridge;

public sealed class SmartFridgeBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private SmartFridgeMenu? _menu;

    public SmartFridgeBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();


        _menu = new SmartFridgeMenu();

        _menu.OnClose += Close;
        _menu.OpenCenteredLeft();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);


        if (!disposing)
            return;

        if (_menu == null)
            return;

        _menu.OnClose -= Close;
        _menu.Dispose();
    }

    // protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    // {
    //     base.ReceiveMessage(message);

    //     if (message is SmartFridgeUpdateInventoryMessage updateInventoryMessage && _menu != null)
    //     {
    //         var smartFridge = EntMan.System<SmartFridgeSystem>();
    //         var inventory = smartFridge.GetSortedInventory(Owner);
    //         _menu.Populate(inventory);
    //     }
    // }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not SmartFridgeBountUserInterfaceState smartFridgeState)
        {
            return;
        }

        if (_menu == null)
        {
            return;
        }

        _menu.Populate(smartFridgeState.Inventory);
    }
}
