namespace GolfPuttingSG.Views;

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
}
