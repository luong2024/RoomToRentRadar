using Microsoft.Extensions.DependencyInjection;
using RoomToRentRadar.Datastore.Sqlite.Configurations;
using RoomToRentRadar.Datastore.Sqlite.Context;
using RoomToRentRadar.Datastore.Sqlite.Repositories;
using RoomToRentRadar.Datastore.Sqlite.Transactions;
using RoomToRentRadar.Ports.Outbound.Repositories;
using RoomToRentRadar.Ports.Outbound.Transactions;

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
            services.AddScoped<IRoomRepository, SqliteRoomRepository>();
            services.AddScoped<IRoomPhotoRepository, SqliteRoomPhotoRepository>();
            services.AddScoped<IUnitOfWork, SqliteUnitOfWork>();
            services.AddScoped<ICampusRepository, SqliteCampusRepository>();
            services.AddScoped<IRoomSearchReadModel, SqliteRoomSearchReadModel>();

            return services;
        }
    }
}
