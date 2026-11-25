using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Utilities;
using RoomToRentRadar.PageModels.Base;
using RoomToRentRadar.Ports.Contracts.Responses.Campuses;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.Services.Navigation.Core;
using ViewState = RoomToRentRadar.PageModels.Base.ViewState;

namespace RoomToRentRadar.PageModels;

/// <summary>One campus row of SCR-ACC-02.</summary>
public sealed partial class CampusRowItem : ObservableObject
{
    public CampusRowItem(CampusDto campus)
    {
        ArgumentNullException.ThrowIfNull(campus);
        Id = campus.Id;
        Code = campus.ShortName;
        Name = campus.Name;
    }

    public string Id { get; }

    public string Code { get; }

    public string Name { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibilityText))]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool ShowDivider { get; set; }

    /// <summary>Screen-reader text for the whole row (NFR accessibility).</summary>
    public string AccessibilityText => IsSelected
        ? $"{Code}, {Name}, {AppResources.GetString("Campus_Row_Selected")}"
        : $"{Code}, {Name}";

    public void RefreshLocalizedText() => OnPropertyChanged(nameof(AccessibilityText));
}

/// <summary>
/// SCR-ACC-02 campus picker (US01, US09; BR-CMP-01..03, UC-24). The tapped row is only a draft until
/// "Use this campus" saves it through <see cref="ICampusSelectionService"/>.
/// </summary>
public partial class CampusPickerPageModel : BasePageModel
{
    private readonly ICampusSelectionService _campusSelection;
    private readonly INavigationService _navigation;
    private IReadOnlyList<CampusDto> _campuses = Array.Empty<CampusDto>();
    private Exception? _saveException;

    public CampusPickerPageModel(ICampusSelectionService campusSelection, INavigationService navigation)
    {
        _campusSelection = campusSelection ?? throw new ArgumentNullException(nameof(campusSelection));
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        SelectedCampusId = _campusSelection.SelectedCampusId;
    }

    public ObservableCollection<CampusRowItem> VisibleCampuses { get; } = [];

    [ObservableProperty]
    public partial string SelectedCampusId { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMatches))]
    public partial bool HasNoMatches { get; set; }

    public bool HasMatches => !HasNoMatches;

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSaveError))]
    public partial string SaveErrorMessage { get; set; } = string.Empty;

    public bool HasSaveError => !string.IsNullOrEmpty(SaveErrorMessage);

    /// <summary>The page draws its own row-shaped skeleton instead of the shared card skeleton.</summary>
    public bool IsSkeletonVisible => State == ViewState.Loading;

    public bool IsBodyVisible => !IsSkeletonVisible;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(State))
        {
            OnPropertyChanged(nameof(IsSkeletonVisible));
            OnPropertyChanged(nameof(IsBodyVisible));
        }
    }

    protected override void OnLanguageChanged()
    {
        foreach (var row in VisibleCampuses)
        {
            row.RefreshLocalizedText();
        }

        if (_saveException is not null)
        {
            SaveErrorMessage = ErrorMessageMapper.Map(_saveException, null);
        }
    }

    public async Task InitializeAsync()
    {
        SelectedCampusId = _campusSelection.SelectedCampusId;

        var cached = _campusSelection.CachedCampuses;
        if (cached is { Count: > 0 })
        {
            // Show the known list at once; a failed refresh then only raises the offline banner.
            ShowCampuses(cached);
            HasContent = true;
            State = ViewState.Content;
            await RunAsync(LoadCampusesAsync, isRefresh: true);
            return;
        }

        await RunAsync(LoadCampusesAsync);
    }

    private async Task<bool> LoadCampusesAsync()
    {
        var campuses = await _campusSelection.GetCampusesAsync(forceRefresh: true);
        ShowCampuses(campuses);
        return campuses.Count > 0;
    }

    private void ShowCampuses(IReadOnlyList<CampusDto> campuses)
    {
        _campuses = campuses;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var matches = CampusFilter.Filter(_campuses, SearchText);

        VisibleCampuses.Clear();
        for (var i = 0; i < matches.Count; i++)
        {
            VisibleCampuses.Add(new CampusRowItem(matches[i])
            {
                IsSelected = IsDraftSelection(matches[i].Id),
                ShowDivider = i < matches.Count - 1
            });
        }

        HasNoMatches = _campuses.Count > 0 && matches.Count == 0;
    }

    private bool IsDraftSelection(string campusId) =>
        string.Equals(campusId, SelectedCampusId, StringComparison.Ordinal);

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void SelectCampus(CampusRowItem? row)
    {
        if (row is null || IsSaving)
        {
            return;
        }

        SelectedCampusId = row.Id;
        ClearSaveError();

        foreach (var item in VisibleCampuses)
        {
            item.IsSelected = IsDraftSelection(item.Id);
        }
    }

    [RelayCommand]
    private async Task UseThisCampusAsync()
    {
        if (IsSaving)
        {
            return;
        }

        ClearSaveError();

        if (string.Equals(SelectedCampusId, _campusSelection.SelectedCampusId, StringComparison.Ordinal))
        {
            await _navigation.GoBackAsync();
            return;
        }

        IsSaving = true;
        try
        {
            await _campusSelection.SelectCampusAsync(SelectedCampusId);
        }
        catch (Exception ex)
        {
            // Stay on the page with the draft kept so the user can retry.
            _saveException = ex;
            SaveErrorMessage = ErrorMessageMapper.Map(ex, null);
            return;
        }
        finally
        {
            IsSaving = false;
        }

        await _navigation.GoBackAsync();
    }

    [RelayCommand]
    private Task CloseAsync() =>
        // Leaving during a save would make the success path pop a second page.
        IsSaving ? Task.CompletedTask : _navigation.GoBackAsync();

    private void ClearSaveError()
    {
        _saveException = null;
        SaveErrorMessage = string.Empty;
    }
}
