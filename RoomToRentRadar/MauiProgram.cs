using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Refit;
using Syncfusion.Maui.Toolkit.Hosting;



namespace RoomToRentRadar
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureSyncfusionToolkit()
#if ANDROID || IOS || MACCATALYST
                .UseMauiMaps()
#endif
                .ConfigureMauiHandlers(handlers =>
                {
#if WINDOWS
    				Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("KeyboardAccessibleCollectionView", (handler, view) =>
    				{
    					handler.PlatformView.SingleSelectionFollowsFocus = false;
    				});

    				Microsoft.Maui.Handlers.ContentViewHandler.Mapper.AppendToMapping(nameof(Pages.Controls.CategoryChart), (handler, view) =>
    				{
    					if (view is Pages.Controls.CategoryChart && handler.PlatformView is Microsoft.Maui.Platform.ContentPanel contentPanel)
    					{
    						contentPanel.IsTabStop = true;
    					}
    				});
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                });

#if DEBUG
    		builder.Logging.AddDebug();
    		builder.Services.AddLogging(configure => configure.AddDebug());
#endif

            builder.Services.AddSingleton<ProjectRepository>();
            builder.Services.AddSingleton<TaskRepository>();
            builder.Services.AddSingleton<CategoryRepository>();
            builder.Services.AddSingleton<TagRepository>();
            builder.Services.AddSingleton<SeedDataService>();
            builder.Services.AddSingleton<ModalErrorHandler>();
            builder.Services.AddSingleton<MainPageModel>();
            builder.Services.AddSingleton<ProjectListPageModel>();
            builder.Services.AddSingleton<ManageMetaPageModel>();

            builder.Services.AddSingleton<INavigationService, MauiNavigationService>();

            // US01 / T01.2: Refit API + Mock fallback + RoomListViewModel
            builder.Services
                .AddRefitClient<IListingsApiService>()
                .ConfigureHttpClient(client =>
                {
                    var baseUrl = DeviceInfo.Platform == DevicePlatform.Android
                        ? "http://10.0.2.2:5000"
                        : "http://localhost:5000";
                    client.BaseAddress = new Uri(baseUrl);
                    client.Timeout = TimeSpan.FromSeconds(5);
                });
            builder.Services.AddSingleton<MockListingsService>();
            builder.Services.AddSingleton<IListingsService, ListingsService>();
            builder.Services.AddSingleton<RoomListViewModel>();
            builder.Services.AddSingleton<RoomListPage>();

            builder.Services.AddTransientWithShellRoute<ProjectDetailPage, ProjectDetailPageModel>("project");
            builder.Services.AddTransientWithShellRoute<TaskDetailPage, TaskDetailPageModel>("task");
            // US05 / T05.1: Spatial Search API + Mock + MapSearchViewModel/Page
            builder.Services
                .AddRefitClient<ISpatialSearchApiService>()
                .ConfigureHttpClient(client =>
                {
                    var baseUrl = DeviceInfo.Platform == DevicePlatform.Android
                        ? "http://10.0.2.2:5000"
                        : "http://localhost:5000";
                    client.BaseAddress = new Uri(baseUrl);
                    client.Timeout = TimeSpan.FromSeconds(5);
                });
            builder.Services.AddSingleton<MockSpatialSearchService>();
            builder.Services.AddSingleton<ISpatialSearchService, SpatialSearchService>();
            builder.Services.AddSingleton<MapSearchViewModel>();
            builder.Services.AddSingleton<MapSearchPage>();

            // US02 / T02.2 & US04 / T04.2: RoomDetailPage & FilterBottomSheet
            builder.Services.AddSingleton<RoomDetailViewModel>();
            builder.Services.AddTransient<RoomDetailPage>();
            builder.Services.AddSingleton<SearchFilterViewModel>();
            builder.Services.AddTransient<FilterBottomSheet>();

            // US03 / T03.2: Photo Gallery API + Mock + Modal & 360 Viewer
            builder.Services
                .AddRefitClient<IPhotoGalleryApiService>()
                .ConfigureHttpClient(client =>
                {
                    var baseUrl = DeviceInfo.Platform == DevicePlatform.Android
                        ? "http://10.0.2.2:5000"
                        : "http://localhost:5000";
                    client.BaseAddress = new Uri(baseUrl);
                    client.Timeout = TimeSpan.FromSeconds(5);
                });
            builder.Services.AddSingleton<MockPhotoGalleryService>();
            builder.Services.AddSingleton<IPhotoGalleryService, PhotoGalleryService>();
            builder.Services.AddTransient<PhotoGalleryViewModel>();
            builder.Services.AddTransient<PhotoGalleryModal>();
            builder.Services.AddTransient<Viewer360Page>();

            return builder.Build();
        }
    }
}
