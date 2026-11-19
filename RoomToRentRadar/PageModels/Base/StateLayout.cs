namespace RoomToRentRadar.PageModels.Base;

public readonly record struct StateLayout(
    bool ShowSkeleton,
    bool ShowContent,
    bool ShowEmpty,
    bool ShowFullError,
    bool ShowInlineError,
    bool ShowBanner)
{
    public static StateLayout Resolve(ViewState state, bool hasContent) => state switch
    {
        ViewState.Loading => new StateLayout(
            ShowSkeleton: true,
            ShowContent: false,
            ShowEmpty: false,
            ShowFullError: false,
            ShowInlineError: false,
            ShowBanner: false),
        ViewState.Content => new StateLayout(
            ShowSkeleton: false,
            ShowContent: true,
            ShowEmpty: false,
            ShowFullError: false,
            ShowInlineError: false,
            ShowBanner: false),
        ViewState.Empty => new StateLayout(
            ShowSkeleton: false,
            ShowContent: false,
            ShowEmpty: true,
            ShowFullError: false,
            ShowInlineError: false,
            ShowBanner: false),
        ViewState.Error when !hasContent => new StateLayout(
            ShowSkeleton: false,
            ShowContent: false,
            ShowEmpty: false,
            ShowFullError: true,
            ShowInlineError: false,
            ShowBanner: false),
        ViewState.Error => new StateLayout(
            ShowSkeleton: false,
            ShowContent: true,
            ShowEmpty: false,
            ShowFullError: false,
            ShowInlineError: true,
            ShowBanner: false),
        ViewState.Offline => new StateLayout(
            ShowSkeleton: false,
            ShowContent: hasContent,
            ShowEmpty: false,
            ShowFullError: false,
            ShowInlineError: false,
            ShowBanner: true),
        _ => new StateLayout(
            ShowSkeleton: false,
            ShowContent: false,
            ShowEmpty: false,
            ShowFullError: true,
            ShowInlineError: false,
            ShowBanner: false)
    };
}
