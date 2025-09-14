
namespace IHFiction.SharedWeb.Services;

public sealed class LoaderService
{
    public event EventHandler? OnShow;
    public event EventHandler? OnHide;

    public bool IsLoading { get; private set; }

    public void Show()
    {
        if (!IsLoading)
        {
            IsLoading = true;
            OnShow?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Hide()
    {
        if (IsLoading)
        {
            IsLoading = false;
            OnHide?.Invoke(this, EventArgs.Empty);
        }
    }
}