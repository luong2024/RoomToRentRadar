using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
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

            builder.Services.AddTransientWithShellRoute<ProjectDetailPage, ProjectDetailPageModel>("project");
            builder.Services.AddTransientWithShellRoute<TaskDetailPage, TaskDetailPageModel>("task");

            // =========================================================================
            // ROOMTORENTRADAR SPRINT 1 CORE FOUNDATION (LOCKED BY LEAD DEV QUANG / @architect)
            // =========================================================================
            builder.Services.AddSingleton<RentalDatabaseService>();
            builder.Services.AddSingleton<ComparisonBasketService>();

            // --- [ZONE 1: LEAD DEV NGUYEN DANG QUANG - T01.1, T04.1, T07.1, T09.1, T10.2, T12.2] ---
            builder.Services.AddSingleton<IPhotoGalleryService, PhotoGalleryService>();  // T04.1 (singleton: shares the write lock)
            builder.Services.AddSingleton<IFavoriteRepository, FavoriteRepository>();      // T07.1

            // --- [ZONE 2: DEV LAM MINH HIEU - T02.2, T04.2, T06.1, T07.2, T09.2, T11.1, T13.1] ---

            // --- [ZONE 3: DEV PHAM THI THUY - T02.1, T03.1, T05.1, T08.1, T10.1, T11.2, T13.2] ---

            // --- [ZONE 4: DEV LIEU HAI NAM - T01.2, T03.2, T05.2, T06.2, T08.2, T12.1] ---

            return builder.Build();
        }
    }
}
