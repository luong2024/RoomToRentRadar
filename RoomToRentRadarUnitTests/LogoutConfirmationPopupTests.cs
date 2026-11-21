using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using NUnit.Framework;
using RoomToRentRadar.Core.Localization;
using RoomToRentRadar.Views.Authentication;

namespace RoomToRentRadarUnitTests;

[TestFixture]
public class LogoutConfirmationPopupTests
{
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        if (Application.Current == null)
        {
            var app = new Application();
            app.Resources = new ResourceDictionary
            {
                { "CardBackground", Colors.White },
                { "CardBackgroundDark", Colors.Black },
                { "BorderSubtle", Colors.LightGray },
                { "BorderSubtleDark", Colors.DarkGray },
                { "PrimaryBrandSoft", Colors.LightBlue },
                { "PrimaryBrandSoftDark", Colors.DarkBlue },
                { "PrimaryBrand", Colors.Blue },
                { "PrimaryBrandDark", Colors.LightSkyBlue },
                { "TextPrimarySlate", Colors.Black },
                { "TextPrimarySlateDark", Colors.White },
                { "TextMutedSlate", Colors.Gray },
                { "TextMutedSlateDark", Colors.LightGray },
                { "White", Colors.White },
                { "Black", Colors.Black }
            };
            Application.Current = app;
        }
    }

    [Test]
    public async Task WaitForDecisionAsync_WhenConfirmClicked_ShouldResolveTrue()
    {
        var popup = new LogoutConfirmationPopup();
        var task = popup.WaitForDecisionAsync();

        // Invoke private OnConfirmClicked via reflection
        var confirmMethod = typeof(LogoutConfirmationPopup).GetMethod("OnConfirmClicked", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(confirmMethod, Is.Not.Null);

        confirmMethod!.Invoke(popup, new object?[] { null, EventArgs.Empty });

        var result = await task;
        Assert.That(result, Is.True);
    }

    [Test]
    public async Task WaitForDecisionAsync_WhenCancelClicked_ShouldResolveFalse()
    {
        var popup = new LogoutConfirmationPopup();
        var task = popup.WaitForDecisionAsync();

        // Invoke private OnCancelClicked via reflection
        var cancelMethod = typeof(LogoutConfirmationPopup).GetMethod("OnCancelClicked", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(cancelMethod, Is.Not.Null);

        cancelMethod!.Invoke(popup, new object?[] { null, EventArgs.Empty });

        var result = await task;
        Assert.That(result, Is.False);
    }

    [Test]
    public async Task WaitForDecisionAsync_WhenPopupClosed_ShouldResolveFalse()
    {
        var popup = new LogoutConfirmationPopup();
        var task = popup.WaitForDecisionAsync();

        // Invoke private OnPopupClosed via reflection
        var closedMethod = typeof(LogoutConfirmationPopup).GetMethod("OnPopupClosed", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(closedMethod, Is.Not.Null);

        closedMethod!.Invoke(popup, new object?[] { null, EventArgs.Empty });

        var result = await task;
        Assert.That(result, Is.False);
    }

    [Test]
    public void Localization_LogoutStrings_ShouldBePresentInEnglishAndVietnamese()
    {
        var loc = LocalizationResourceManager.Instance;

        loc.SetCulture(new System.Globalization.CultureInfo("en"));
        Assert.That(loc["Nav_Logout"], Is.EqualTo("Log Out"));
        Assert.That(loc["Logout_Confirm_Title"], Is.EqualTo("Log Out"));
        Assert.That(loc["Logout_Confirm_Message"], Is.EqualTo("Are you sure you want to log out?"));
        Assert.That(loc["Logout_Confirm_Subtitle"], Is.EqualTo("After logging out, you will need to sign in again to continue using the system."));
        Assert.That(loc["Logout_Confirm_Yes"], Is.EqualTo("Log Out"));
        Assert.That(loc["Logout_Confirm_No"], Is.EqualTo("Cancel"));

        loc.SetCulture(new System.Globalization.CultureInfo("vi"));
        Assert.That(loc["Nav_Logout"], Is.EqualTo("Đăng xuất"));
        Assert.That(loc["Logout_Confirm_Title"], Is.EqualTo("Đăng xuất"));
        Assert.That(loc["Logout_Confirm_Message"], Is.EqualTo("Bạn có chắc chắn muốn đăng xuất?"));
        Assert.That(loc["Logout_Confirm_Subtitle"], Is.EqualTo("Sau khi đăng xuất, bạn sẽ cần đăng nhập lại để tiếp tục sử dụng hệ thống."));
        Assert.That(loc["Logout_Confirm_Yes"], Is.EqualTo("Đăng xuất"));
        Assert.That(loc["Logout_Confirm_No"], Is.EqualTo("Hủy"));

        // Reset to en
        loc.SetCulture(new System.Globalization.CultureInfo("en"));
    }
}
