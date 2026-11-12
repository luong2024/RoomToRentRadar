using RoomToRentRadar.Bootstrap.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// TODO: Cấu hình Authentication đầy đủ (JwtBearer, cookies, v.v.) khi hoàn thiện hệ thống Auth chung
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

builder.Services
    .AddRoomDetailServices(builder.Configuration, builder.Environment)
    .AddInboundAdapters();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
