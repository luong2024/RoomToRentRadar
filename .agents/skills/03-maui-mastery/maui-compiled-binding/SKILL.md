---
name: maui-compiled-binding
description: Enforces Compiled Bindings (x:DataType) to maximize runtime rendering performance and catch binding errors at build time.
---

# MAUI Compiled Bindings Best Practices

Derived from `davidortinau/maui-skills/maui-data-binding`. Enables compile-time type checking in XAML, replacing slow reflection-based bindings with fast generated code and catching property typos at build time.

## 1. Mandatory Root Page Declaration
At the root of every `ContentPage`, declare the XML namespace and ViewModel `x:DataType`:

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:vm="clr-namespace:RoomToRentRadar.PageModels"
             x:Class="RoomToRentRadar.Pages.RoomListPage"
             x:DataType="vm:RoomListViewModel"
             Title="Find Rooms Near ICTU">
    ...
</ContentPage>
```

## 2. Mandatory Declaration in All DataTemplates
Within every `CollectionView`, `CarouselView`, or `ListView`, every `DataTemplate` **MUST** declare the concrete item data type:

```xml
<!-- CORRECT: Compiled binding enabled, high performance, compile-time property validation -->
<CollectionView.ItemTemplate>
    <DataTemplate x:DataType="contracts:RoomCardDto">
        <Border Padding="10" Margin="5">
            <Label Text="{Binding Title}" FontAttributes="Bold" />
            <Label Text="{Binding MonthlyRent, StringFormat='{0:N0} VND/month'}" TextColor="Red" />
        </Border>
    </DataTemplate>
</CollectionView.ItemTemplate>

<!-- INCORRECT: Missing x:DataType causes reflection overhead and silent runtime binding failures -->
```
