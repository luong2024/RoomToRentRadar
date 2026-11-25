using System.Globalization;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using RoomToRentRadar.Client.Adapters.Abstractions;
using RoomToRentRadar.Client.Adapters.Mocks.SampleData;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.PageModels.Base;
using RoomToRentRadar.Ports.Contracts.Responses.Campuses;
using RoomToRentRadar.Resources.Strings;
using RoomToRentRadar.Services.Navigation.Core;

namespace RoomToRentRadarUnitTests.PageModels;

/// <summary>T13.4 — SCR-ACC-02 campus picker.</summary>
[TestFixture]
public class CampusPickerPageModelTests
{
    private ICampusSelectionService _service = null!;
    private INavigationService _navigation = null!;

    [SetUp]
    public void SetUp()
    {
        LocalizedStrings.Instance.SetCulture(new CultureInfo("en-US"));
        _service = Substitute.For<ICampusSelectionService>();
        _service.SelectedCampusId.Returns("ICTU");
        _service.CachedCampuses.Returns((IReadOnlyList<CampusDto>?)null);
        _service.GetCampusesAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(SampleCampuses.All);
        _navigation = Substitute.For<INavigationService>();
    }

    [TearDown]
    public void TearDown() => LocalizedStrings.Instance.SetCulture(new CultureInfo("en-US"));

    private async Task<CampusPickerPageModel> CreateLoadedAsync()
    {
        var model = new CampusPickerPageModel(_service, _navigation);
        await model.InitializeAsync();
        return model;
    }

    private void LoadFails(ApiException error) =>
        _service.GetCampusesAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).ThrowsAsync(error);

    private static ApiException NetworkError() => new("offline", 0, ApiErrorCodes.NetworkError);

    private static CampusRowItem Row(CampusPickerPageModel model, string id) =>
        model.VisibleCampuses.Single(r => r.Id == id);

    private Task AssertNotSelectedAsync() =>
        _service.DidNotReceive().SelectCampusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

    [Test]
    public async Task Initialize_LoadsCampuses_ShowsContent_PreselectsCurrent()
    {
        _service.SelectedCampusId.Returns("TNUT");

        var model = await CreateLoadedAsync();

        Assert.Multiple(() =>
        {
            Assert.That(model.State, Is.EqualTo(ViewState.Content));
            Assert.That(model.IsSkeletonVisible, Is.False);
            Assert.That(model.VisibleCampuses, Has.Count.EqualTo(8));
            Assert.That(model.VisibleCampuses[0].Id, Is.EqualTo("ICTU"));
            Assert.That(model.VisibleCampuses.Where(r => r.IsSelected).Select(r => r.Id), Is.EqualTo(new[] { "TNUT" }));
            Assert.That(model.VisibleCampuses.Count(r => r.ShowDivider), Is.EqualTo(7));
        });
    }

    [Test]
    public async Task Initialize_GatewayFails_ErrorState_RetryReloads()
    {
        _service.GetCampusesAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(
            _ => Task.FromException<IReadOnlyList<CampusDto>>(NetworkError()),
            _ => Task.FromResult(SampleCampuses.All));

        var model = await CreateLoadedAsync();
        Assert.That(model.State, Is.EqualTo(ViewState.Error));
        Assert.That(model.ErrorMessage, Is.Not.Empty);

        await model.RetryCommand.ExecuteAsync(null);

        Assert.That(model.State, Is.EqualTo(ViewState.Content));
        Assert.That(model.VisibleCampuses, Has.Count.EqualTo(8));
    }

    [Test]
    public async Task Initialize_NetworkErrorWithCachedList_OfflineState()
    {
        _service.CachedCampuses.Returns(SampleCampuses.All);
        LoadFails(NetworkError());

        var model = await CreateLoadedAsync();

        Assert.Multiple(() =>
        {
            Assert.That(model.State, Is.EqualTo(ViewState.Offline));
            Assert.That(model.HasContent, Is.True);
            Assert.That(model.VisibleCampuses, Has.Count.EqualTo(8));
        });
    }

    [Test]
    public async Task Initialize_ZeroCampuses_EmptyState()
    {
        _service.GetCampusesAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<CampusDto>());

        var model = await CreateLoadedAsync();

        Assert.That(model.State, Is.EqualTo(ViewState.Empty));
        Assert.That(model.HasNoMatches, Is.False);
    }

    [Test]
    public async Task SearchText_FiltersVisibleCampuses_NoNetworkCall()
    {
        var model = await CreateLoadedAsync();
        _service.ClearReceivedCalls();

        model.SearchText = "kinh te";

        Assert.That(model.VisibleCampuses.Select(r => r.Id), Is.EqualTo(new[] { "TUEBA" }));
        await _service.DidNotReceive().GetCampusesAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SearchText_NoMatch_SetsHasNoMatches_ClearingRestoresAll()
    {
        var model = await CreateLoadedAsync();

        model.SearchText = "xyz";
        Assert.That(model.HasNoMatches, Is.True);
        Assert.That(model.HasMatches, Is.False);

        model.SearchText = string.Empty;
        Assert.That(model.HasNoMatches, Is.False);
        Assert.That(model.VisibleCampuses, Has.Count.EqualTo(8));
    }

    [Test]
    public async Task SelectCampus_UpdatesTemporarySelectionOnly()
    {
        var model = await CreateLoadedAsync();

        model.SelectCampusCommand.Execute(Row(model, "TUMP"));

        Assert.Multiple(() =>
        {
            Assert.That(model.SelectedCampusId, Is.EqualTo("TUMP"));
            Assert.That(Row(model, "TUMP").IsSelected, Is.True);
            Assert.That(Row(model, "ICTU").IsSelected, Is.False);
        });
        await AssertNotSelectedAsync();
        await _navigation.DidNotReceive().GoBackAsync();
    }

    [Test]
    public async Task SelectCampus_SurvivesFiltering()
    {
        var model = await CreateLoadedAsync();
        model.SelectCampusCommand.Execute(Row(model, "TUMP"));

        model.SearchText = "y duoc";

        Assert.That(Row(model, "TUMP").IsSelected, Is.True);
    }

    [Test]
    public async Task UseThisCampus_Success_CallsServiceAndGoesBack()
    {
        var model = await CreateLoadedAsync();
        model.SelectCampusCommand.Execute(Row(model, "TNUT"));

        await model.UseThisCampusCommand.ExecuteAsync(null);

        await _service.Received(1).SelectCampusAsync("TNUT", Arg.Any<CancellationToken>());
        await _navigation.Received(1).GoBackAsync();
        Assert.That(model.IsSaving, Is.False);
    }

    [Test]
    public async Task UseThisCampus_SameAsCurrent_JustGoesBack_NoServiceCall()
    {
        var model = await CreateLoadedAsync();

        await model.UseThisCampusCommand.ExecuteAsync(null);

        await AssertNotSelectedAsync();
        await _navigation.Received(1).GoBackAsync();
    }

    [Test]
    public async Task UseThisCampus_ServiceThrows_ShowsMappedError_StaysOnPage_CanRetry()
    {
        _service.SelectCampusAsync("TNUT", Arg.Any<CancellationToken>()).Returns(
            _ => Task.FromException(NetworkError()),
            _ => Task.CompletedTask);
        var model = await CreateLoadedAsync();
        model.SelectCampusCommand.Execute(Row(model, "TNUT"));

        await model.UseThisCampusCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(model.SaveErrorMessage, Is.EqualTo(AppResources.GetString("Error_NETWORK_ERROR")));
            Assert.That(model.HasSaveError, Is.True);
            Assert.That(model.SelectedCampusId, Is.EqualTo("TNUT"));
            Assert.That(model.IsSaving, Is.False);
        });
        await _navigation.DidNotReceive().GoBackAsync();

        await model.UseThisCampusCommand.ExecuteAsync(null);

        Assert.That(model.HasSaveError, Is.False);
        await _navigation.Received(1).GoBackAsync();
    }

    [Test]
    public async Task SaveError_IsRelocalized_WhenLanguageChanges()
    {
        _service.SelectCampusAsync("TNUT", Arg.Any<CancellationToken>()).ThrowsAsync(NetworkError());
        var model = await CreateLoadedAsync();
        model.SelectCampusCommand.Execute(Row(model, "TNUT"));
        await model.UseThisCampusCommand.ExecuteAsync(null);
        var english = model.SaveErrorMessage;

        LocalizedStrings.Instance.SetCulture(new CultureInfo("vi-VN"));

        Assert.Multiple(() =>
        {
            Assert.That(model.SaveErrorMessage, Is.Not.EqualTo(english));
            Assert.That(Row(model, "TNUT").AccessibilityText, Does.EndWith("đã chọn"));
        });
        GC.KeepAlive(model);
    }

    [Test]
    public async Task UseThisCampus_WhileSaving_IgnoresSecondTapSelectionAndClose()
    {
        var saving = new TaskCompletionSource();
        _service.SelectCampusAsync("TNUT", Arg.Any<CancellationToken>()).Returns(saving.Task);
        var model = await CreateLoadedAsync();
        model.SelectCampusCommand.Execute(Row(model, "TNUT"));

        var first = model.UseThisCampusCommand.ExecuteAsync(null);
        Assert.That(model.IsSaving, Is.True);
        await model.UseThisCampusCommand.ExecuteAsync(null);
        model.SelectCampusCommand.Execute(Row(model, "TUMP"));
        await model.CloseCommand.ExecuteAsync(null);

        Assert.That(model.SelectedCampusId, Is.EqualTo("TNUT"));
        await _navigation.DidNotReceive().GoBackAsync();

        saving.SetResult();
        await first;

        await _service.Received(1).SelectCampusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _navigation.Received(1).GoBackAsync();
    }

    [Test]
    public async Task Close_GoesBackWithoutSaving()
    {
        var model = await CreateLoadedAsync();
        model.SelectCampusCommand.Execute(Row(model, "TNUT"));

        await model.CloseCommand.ExecuteAsync(null);

        await _navigation.Received(1).GoBackAsync();
        await AssertNotSelectedAsync();
    }

    [TestCase("en-US")]
    [TestCase("vi-VN")]
    public void CampusResourceKeys_ExistInBothLanguages(string culture)
    {
        string[] keys =
        [
            "Campus_Title", "Campus_Subtitle", "Campus_Search_Placeholder", "Campus_UseThisCampus",
            "Campus_Close_Description", "Campus_Row_Selected", "Campus_NoMatch_Title",
            "Campus_NoMatch_Message", "Campus_Load_Error_Title", "Campus_Empty_Title", "Campus_Empty_Message"
        ];
        var english = new CultureInfo("en-US");
        var target = new CultureInfo(culture);

        Assert.Multiple(() =>
        {
            foreach (var key in keys)
            {
                var value = AppResources.GetString(key, target);
                Assert.That(value, Is.Not.Empty.And.Not.EqualTo(key).And.Not.StartsWith("["), key);
                if (culture != "en-US")
                {
                    Assert.That(value, Is.Not.EqualTo(AppResources.GetString(key, english)), $"{key} is not translated.");
                }
            }
        });
    }
}
