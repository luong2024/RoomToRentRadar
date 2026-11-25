using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoomToRentRadar.Adapters.Outbound.Persistence.CosmosDb.Repositories;
using RoomToRentRadar.Application.Ports.Inbound;
using RoomToRentRadar.Application.Ports.Outbound;
using RoomToRentRadar.Application.UseCases.Reports;

namespace RoomToRentRadar.Bootstrap.DependencyInjection;

public static class ListingReportServiceRegistration
{
    public static IServiceCollection AddListingReportServices(this IServiceCollection services)
    {
        services.AddScoped<IListingReportRepository, CosmosListingReportRepository>();
        services.AddScoped<IReportSubmissionRule, PreventSelfReportRule>();
        services.AddScoped<IReportSubmissionRule, PreventDuplicateActiveReportRule>();
        services.TryAddScoped<ISubmitListingReportUseCase, SubmitListingReportUseCase>();
        services.TryAddScoped<IGetModerationQueueUseCase, GetModerationQueueUseCase>();
        services.TryAddScoped<IReviewListingReportUseCase, ReviewListingReportUseCase>();

        return services;
    }
}
