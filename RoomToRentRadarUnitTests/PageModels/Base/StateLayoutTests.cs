using NUnit.Framework;
using RoomToRentRadar.PageModels.Base;

namespace RoomToRentRadarUnitTests.PageModels.Base;

[TestFixture]
public class StateLayoutTests
{
    private static void AssertLayout(
        StateLayout layout,
        bool skeleton,
        bool content,
        bool empty,
        bool fullError,
        bool inlineError,
        bool banner)
    {
        Assert.Multiple(() =>
        {
            Assert.That(layout.ShowSkeleton, Is.EqualTo(skeleton), nameof(layout.ShowSkeleton));
            Assert.That(layout.ShowContent, Is.EqualTo(content), nameof(layout.ShowContent));
            Assert.That(layout.ShowEmpty, Is.EqualTo(empty), nameof(layout.ShowEmpty));
            Assert.That(layout.ShowFullError, Is.EqualTo(fullError), nameof(layout.ShowFullError));
            Assert.That(layout.ShowInlineError, Is.EqualTo(inlineError), nameof(layout.ShowInlineError));
            Assert.That(layout.ShowBanner, Is.EqualTo(banner), nameof(layout.ShowBanner));
        });
    }

    [Test]
    public void Resolve_Loading_ShowsSkeletonOnly()
    {
        var layout = StateLayout.Resolve(ViewState.Loading, hasContent: false);

        AssertLayout(layout, skeleton: true, content: false, empty: false, fullError: false, inlineError: false, banner: false);
    }

    [Test]
    public void Resolve_Content_ShowsContentOnly()
    {
        var layout = StateLayout.Resolve(ViewState.Content, hasContent: true);

        AssertLayout(layout, skeleton: false, content: true, empty: false, fullError: false, inlineError: false, banner: false);
    }

    [Test]
    public void Resolve_Empty_ShowsEmptyOnly()
    {
        var layout = StateLayout.Resolve(ViewState.Empty, hasContent: false);

        AssertLayout(layout, skeleton: false, content: false, empty: true, fullError: false, inlineError: false, banner: false);
    }

    [Test]
    public void Resolve_ErrorWithoutContent_ShowsFullError()
    {
        var layout = StateLayout.Resolve(ViewState.Error, hasContent: false);

        AssertLayout(layout, skeleton: false, content: false, empty: false, fullError: true, inlineError: false, banner: false);
    }

    [Test]
    public void Resolve_ErrorWithContent_ShowsContentAndInlineError()
    {
        var layout = StateLayout.Resolve(ViewState.Error, hasContent: true);

        AssertLayout(layout, skeleton: false, content: true, empty: false, fullError: false, inlineError: true, banner: false);
    }

    [Test]
    public void Resolve_Offline_ShowsContentAndBanner()
    {
        var layout = StateLayout.Resolve(ViewState.Offline, hasContent: true);

        AssertLayout(layout, skeleton: false, content: true, empty: false, fullError: false, inlineError: false, banner: true);
    }
}
