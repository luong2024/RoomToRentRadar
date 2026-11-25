namespace RoomToRentRadar.Pages.Controls.RoomDetail;

public partial class LocationTravelTimesControl : ContentView
{
    public LocationTravelTimesControl()
    {
        InitializeComponent();
    }

    private void OnCommuteGridChildAdded(object? sender, ElementEventArgs e) => SyncColumns();

    private void OnCommuteGridChildRemoved(object? sender, ElementEventArgs e) => SyncColumns();

    private void SyncColumns()
    {
        int count = CommuteGrid.Children.Count;
        if (count == 0) return;

        if (CommuteGrid.ColumnDefinitions.Count != count)
        {
            var cols = new ColumnDefinitionCollection();
            for (int i = 0; i < count; i++)
            {
                cols.Add(new ColumnDefinition(GridLength.Star));
            }
            CommuteGrid.ColumnDefinitions = cols;
        }

        for (int i = 0; i < count; i++)
        {
            if (CommuteGrid.Children[i] is BindableObject child)
            {
                Grid.SetColumn(child, i);
            }
        }
    }
}
