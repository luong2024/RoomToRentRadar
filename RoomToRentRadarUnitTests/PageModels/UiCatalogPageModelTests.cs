using System.Globalization;
using RoomToRentRadar.Converters;
using RoomToRentRadar.PageModels;
using RoomToRentRadar.Resources.Strings;

namespace RoomToRentRadarUnitTests.PageModels;

[TestFixture]
[NonParallelizable]
public class UiCatalogPageModelTests
{
    private CultureInfo _originalCulture = null!;
    private CultureInfo _originalUiCulture = null!;
    private CultureInfo? _originalDefaultCulture;
    private CultureInfo? _originalDefaultUiCulture;
    private CultureInfo? _originalResourceCulture;

    [SetUp]
    public void SetUp()
    {
        _originalCulture = Thread.CurrentThread.CurrentCulture;
        _originalUiCulture = Thread.CurrentThread.CurrentUICulture;
        _originalDefaultCulture = CultureInfo.DefaultThreadCurrentCulture;
        _originalDefaultUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        _originalResourceCulture = AppResources.Culture;
    }

    [TearDown]
    public void TearDown()
    {
        AppResources.Culture = _originalResourceCulture;
        CultureInfo.DefaultThreadCurrentCulture = _originalDefaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = _originalDefaultUiCulture;
        Thread.CurrentThread.CurrentCulture = _originalCulture;
        Thread.CurrentThread.CurrentUICulture = _originalUiCulture;
    }

    [Test]
    public void Constructor_DefaultsToCurrentAppCulture_WithFixedDemoValues()
    {
        LocalizedStrings.Instance.SetCulture(CultureInfo.GetCultureInfo("vi-VN"));

        var vm = new UiCatalogPageModel();

        Assert.That(vm.AppCulture.Name, Is.EqualTo("vi-VN"));
        Assert.That(vm.PreviewCulture.Name, Is.EqualTo("vi-VN"));
        Assert.That(vm.IsUsingAppCulture, Is.True);
        Assert.That(vm.IsManualOverride, Is.False);
        Assert.That(vm.IsVietnamesePreview, Is.True);
        Assert.That(vm.IsEnglishPreview, Is.False);

        Assert.That(vm.DemoCurrencyAmount, Is.EqualTo(2200000m));
        Assert.That(vm.DemoNumberValue, Is.EqualTo(12345.67));
        Assert.That(vm.DemoDateValue, Is.EqualTo(new DateOnly(2026, 11, 25)));
        Assert.That(vm.DemoTimeValue, Is.EqualTo(new TimeOnly(16, 30)));
        Assert.That(vm.DemoAreaValue, Is.EqualTo(25.5));
        Assert.That(vm.DemoDistanceValue, Is.EqualTo(1500d));
        Assert.That(vm.SampleRoom.PricePerMonth, Is.EqualTo(2200000m));
        Assert.That(vm.SampleRoom.AreaSquareMeters, Is.EqualTo(25.5));
        Assert.That(vm.SampleRoom.DistanceMeters, Is.EqualTo(350d));
    }

    [Test]
    public void SelectPreviewCultureCommand_OverridesPreviewLocally_WithoutChangingGlobalCulture()
    {
        LocalizedStrings.Instance.SetCulture(CultureInfo.GetCultureInfo("vi-VN"));
        var vm = new UiCatalogPageModel();
        var changedProps = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

        vm.SelectPreviewCultureCommand.Execute("en-US");

        // Local preview updated
        Assert.That(vm.PreviewCulture.Name, Is.EqualTo("en-US"));
        Assert.That(vm.IsUsingAppCulture, Is.False);
        Assert.That(vm.IsManualOverride, Is.True);
        Assert.That(vm.IsEnglishPreview, Is.True);
        Assert.That(vm.IsVietnamesePreview, Is.False);
        Assert.That(changedProps, Does.Contain(nameof(UiCatalogPageModel.PreviewCulture)));
        Assert.That(changedProps, Does.Contain(nameof(UiCatalogPageModel.IsUsingAppCulture)));

        // Global app culture stays untouched (AccountPageModel.LanguageName not affected)
        Assert.That(vm.AppCulture.Name, Is.EqualTo("vi-VN"));
        Assert.That(CultureInfo.CurrentCulture.Name, Is.EqualTo("vi-VN"));
        Assert.That(AppResources.Culture?.Name, Is.EqualTo("vi-VN"));

        // Raw demo values unchanged while formatted output switches cleanly
        Assert.That(CultureFormatting.FormatCurrency(vm.DemoCurrencyAmount, vm.PreviewCulture), Is.EqualTo("2,200,000 VND"));
        Assert.That(CultureFormatting.FormatDate(vm.DemoDateValue, vm.PreviewCulture), Is.EqualTo("11/25/2026"));
        Assert.That(CultureFormatting.FormatDistance(vm.SampleRoom.DistanceMeters, vm.PreviewCulture), Is.EqualTo("350 m"));
        Assert.That(vm.DemoCurrencyAmount, Is.EqualTo(2200000m));
    }

    [Test]
    public void UseAppCultureCommand_RestoresFollowingAppCultureAndNotifies()
    {
        LocalizedStrings.Instance.SetCulture(CultureInfo.GetCultureInfo("vi-VN"));
        var vm = new UiCatalogPageModel();
        vm.SelectPreviewCultureCommand.Execute("en-US");

        var changedProps = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

        vm.UseAppCultureCommand.Execute(null);

        Assert.That(vm.IsUsingAppCulture, Is.True);
        Assert.That(vm.IsManualOverride, Is.False);
        Assert.That(vm.AppCulture.Name, Is.EqualTo("vi-VN"));
        Assert.That(vm.PreviewCulture.Name, Is.EqualTo("vi-VN"));
        Assert.That(changedProps, Does.Contain(nameof(UiCatalogPageModel.PreviewCulture)));
        Assert.That(changedProps, Does.Contain(nameof(UiCatalogPageModel.IsUsingAppCulture)));
    }

    [Test]
    public void OnAppCultureChanged_WhenFollowingAppCulture_UpdatesPreviewAndNotifies()
    {
        LocalizedStrings.Instance.SetCulture(CultureInfo.GetCultureInfo("en-US"));
        var vm = new UiCatalogPageModel();
        var changedProps = new List<string?>();
        vm.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName);

        LocalizedStrings.Instance.SetCulture(CultureInfo.GetCultureInfo("vi-VN"));
        vm.OnAppCultureChanged();

        Assert.That(vm.AppCulture.Name, Is.EqualTo("vi-VN"));
        Assert.That(vm.PreviewCulture.Name, Is.EqualTo("vi-VN"));
        Assert.That(vm.IsUsingAppCulture, Is.True);
        Assert.That(changedProps, Does.Contain(nameof(UiCatalogPageModel.AppCulture)));
        Assert.That(changedProps, Does.Contain(nameof(UiCatalogPageModel.PreviewCulture)));
    }

    [Test]
    public void OnAppCultureChanged_WhenManualOverrideActive_PreservesPreviewWhileUpdatingAppCulture()
    {
        LocalizedStrings.Instance.SetCulture(CultureInfo.GetCultureInfo("en-US"));
        var vm = new UiCatalogPageModel();
        vm.SelectPreviewCultureCommand.Execute("en-US");

        LocalizedStrings.Instance.SetCulture(CultureInfo.GetCultureInfo("vi-VN"));
        vm.OnAppCultureChanged();

        Assert.That(vm.AppCulture.Name, Is.EqualTo("vi-VN"));
        Assert.That(vm.PreviewCulture.Name, Is.EqualTo("en-US"));
        Assert.That(vm.IsUsingAppCulture, Is.False);
    }

    [Test]
    public void OnAppearing_ResetsManualOverrideAndSyncsWithLatestAppCulture()
    {
        LocalizedStrings.Instance.SetCulture(CultureInfo.GetCultureInfo("vi-VN"));
        var vm = new UiCatalogPageModel();
        vm.SelectPreviewCultureCommand.Execute("en-US");
        Assert.That(vm.IsUsingAppCulture, Is.False);

        // Simulate user switching app language in Account, then navigating back to Catalog
        LocalizedStrings.Instance.SetCulture(CultureInfo.GetCultureInfo("vi-VN"));
        vm.OnAppearing();

        Assert.That(vm.IsUsingAppCulture, Is.True);
        Assert.That(vm.AppCulture.Name, Is.EqualTo("vi-VN"));
        Assert.That(vm.PreviewCulture.Name, Is.EqualTo("vi-VN"));

        // Switch to English in Account and re-enter Catalog
        LocalizedStrings.Instance.SetCulture(CultureInfo.GetCultureInfo("en-US"));
        vm.OnAppearing();

        Assert.That(vm.IsUsingAppCulture, Is.True);
        Assert.That(vm.AppCulture.Name, Is.EqualTo("en-US"));
        Assert.That(vm.PreviewCulture.Name, Is.EqualTo("en-US"));
    }
}
