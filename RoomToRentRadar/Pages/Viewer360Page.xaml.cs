using System;
using System.Collections.Generic;
using Microsoft.Maui.Controls;

namespace RoomToRentRadar.Pages;

public partial class Viewer360Page : ContentPage, IQueryAttributable
{
    public Viewer360Page()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var imageUrl = query.TryGetValue("imageUrl", out var imgObj) && imgObj is string s
            ? s
            : string.Empty;

        if (query.TryGetValue("caption", out var capObj) && capObj is string cap && !string.IsNullOrWhiteSpace(cap))
        {
            CaptionLabel.Text = $"{cap} • Kéo để xoay 360°";
        }

        var encodedImg = Uri.EscapeDataString(imageUrl);
        PanoramaWebView.Source = new UrlWebViewSource
        {
            Url = $"viewer360.html?img={encodedImg}"
        };
    }
}
