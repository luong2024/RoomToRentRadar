using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Features.ListingManagement.Models;

namespace RoomToRentRadar.Features.ListingManagement.ViewModels;

/// <summary>One photo chip in the editor: either already stored or newly picked.</summary>
public sealed partial class EditorPhoto : ObservableObject
{
    private readonly Action<EditorPhoto>? _onRemove;

    public EditorPhoto(
        string fileName,
        bool isNew,
        Guid? photoId,
        ImageSource? preview,
        Func<Task<Stream>>? openReadAsync,
        string? contentType,
        long? length,
        Action<EditorPhoto>? onRemove = null)
    {
        FileName = fileName;
        IsNew = isNew;
        PhotoId = photoId;
        PreviewImageSource = preview;
        OpenReadAsync = openReadAsync;
        ContentType = contentType;
        Length = length;
        _onRemove = onRemove;
    }

    public string FileName { get; }

    public bool IsNew { get; }

    public Guid? PhotoId { get; }

    public string? ContentType { get; }

    public long? Length { get; }

    /// <summary>Repeatable stream factory; null for photos already on the server.</summary>
    public Func<Task<Stream>>? OpenReadAsync { get; }

    /// <summary>Local preview; null when the image cannot be previewed.</summary>
    public ImageSource? PreviewImageSource { get; }

    /// <summary>Photos already uploaded cannot be deleted from the editor (US08 scope).</summary>
    public bool IsRemovable => IsNew;

    public string BadgeText => IsNew ? "Ảnh mới" : "Đã tải lên";

    public ListingPhotoCandidate ToCandidate() => new(FileName, ContentType, Length);

    [RelayCommand]
    private void Remove() => _onRemove?.Invoke(this);
}
