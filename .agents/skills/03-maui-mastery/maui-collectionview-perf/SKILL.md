---
name: maui-collectionview-perf
description: Optimizes CollectionView performance for smooth scrolling with 500 items and infinite scroll support.
---

# MAUI CollectionView Performance & Infinite Scroll

Derived from `davidortinau/maui-skills/maui-collectionview`. Directly satisfies **US01 (Load up to 500 listings within 3 seconds with smooth scrolling)**.

## 1. Optimized XAML Template for Room Listings

```xml
<RefreshView IsRefreshing="{Binding IsRefreshing}" Command="{Binding RefreshCommand}">
    <CollectionView ItemsSource="{Binding Rooms}"
                    RemainingItemsThreshold="5"
                    RemainingItemsThresholdReachedCommand="{Binding LoadMoreRoomsCommand}"
                    ItemSizingStrategy="MeasureFirstItem">
        
        <!-- EmptyState view when no matching rooms are found -->
        <CollectionView.EmptyView>
            <VerticalStackLayout HorizontalOptions="Center" VerticalOptions="Center" Spacing="10">
                <Image Source="empty_rooms.png" WidthRequest="120" HeightRequest="120" />
                <Label Text="No rooms match your search criteria" FontAttributes="Bold" />
                <Button Text="Reset Filters" Command="{Binding ClearFiltersCommand}" />
            </VerticalStackLayout>
        </CollectionView.EmptyView>

        <!-- Room card presentation template -->
        <CollectionView.ItemTemplate>
            <DataTemplate x:DataType="contracts:RoomCardDto">
                <!-- Lightweight card hierarchy using Border and Grid -->
            </DataTemplate>
        </CollectionView.ItemTemplate>
    </CollectionView>
</RefreshView>
```

## 2. Golden Performance Rules
1. **`ItemSizingStrategy="MeasureFirstItem"`:** When room cards have uniform dimensions, this setting reduces layout measurement overhead by up to 70%.
2. **`RemainingItemsThreshold="5"`:** When the user scrolls within 5 items of the end, automatically triggers `LoadMoreRoomsCommand` for seamless pagination (Infinite Scroll).
3. **Never nest a `CollectionView` inside a `ScrollView`:** Doing so disables cell recycling and virtualization, resulting in severe memory bloat and application unresponsiveness.
