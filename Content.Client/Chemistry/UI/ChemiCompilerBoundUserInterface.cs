using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Chemistry.UI;

[UsedImplicitly]
public sealed class ChemiCompilerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ChemiCompilerWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ChemiCompilerWindow>();

        _window.SetReservoirCount(10);
    }
}
