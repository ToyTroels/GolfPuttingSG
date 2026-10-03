namespace GolfSG.Views;

public static class ViewExtensions
{
    public static T Row<T>(this T view, int row) where T : BindableObject
    {
        Grid.SetRow(view, row);
        return view;
    }

    public static T Column<T>(this T view, int column) where T : BindableObject
    {
        Grid.SetColumn(view, column);
        return view;
    }

    public static T Margin<T>(this T view, Thickness margin) where T : View
    {
        view.Margin = margin;
        return view;
    }

    public static T Accessible<T>(
        this T view,
        string automationId,
        string description,
        string? hint = null) where T : VisualElement
    {
        view.AutomationId = automationId;
        SemanticProperties.SetDescription(view, description);
        if (!string.IsNullOrWhiteSpace(hint))
        {
            SemanticProperties.SetHint(view, hint);
        }

        return view;
    }
}
