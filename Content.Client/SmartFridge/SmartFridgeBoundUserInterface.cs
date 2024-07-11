
using Content.Client.SmartFridge.UI;
using Content.Shared.SmartFridge;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BaseButton;

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

        _menu.OnItemDispensed += DispenseItem;
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

        _menu.OnItemDispensed -= DispenseItem;
        _menu.OnClose -= Close;
        _menu.Dispose();
    }

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

    private void DispenseItem(SmartFridgeInventoryEntry entry, int amount)
    {
        Logger.GetSawmill("SmartFridge").Debug($"{PrettyPrint.PrintUserFacing(entry)}: {amount}");

        SendMessage(new SmartFridgeDispenseItemMessage(entry, amount));
    }
}
