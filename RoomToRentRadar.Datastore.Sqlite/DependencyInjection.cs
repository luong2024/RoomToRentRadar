using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Ports.Outbound.Repositories;

namespace RoomToRentRadar.Datastore.Sqlite
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddSqliteDatastore(
            this IServiceCollection services,
            SqliteSettings? settings = null)
        {
            var effectiveSettings = settings ?? new SqliteSettings();
            services.AddSingleton(effectiveSettings);
            services.AddSingleton<SqliteDbContext>();
            services.AddScoped<IUserRepository, SqliteUserRepository>();
            services.AddScoped<IAuditLogRepository, SqliteAuditLogRepository>();
            services.AddScoped<IRoomPhotoRepository, SqliteRoomPhotoRepository>();

            return services;
        }
    }
}

namespace RoomToRentRadar.Datastore
{
    using RoomToRentRadar.Datastore.Sqlite;
    using RoomToRentRadar.Datastore.Sqlite.Configurations;

    public static class SqliteDatastoreCompatibilityExtensions
    {
        public static IServiceCollection AddDatastore(
            this IServiceCollection services,
            SqliteSettings? settings = null) =>
            services.AddSqliteDatastore(settings);
    }
}
