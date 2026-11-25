using System.Net;
using RoomToRentRadar.Models;
using RoomToRentRadar.Services.Api;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Services.Auth;

public sealed class AuthService(IApiClient apiClient, ITokenStore tokenStore)
{
    public AppUserRole? CurrentRole { get; private set; }

    public event EventHandler? SessionChanged;

    public async Task SignInAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var response = await apiClient.LoginAsync(new LoginRequest(email, password), cancellationToken);
        if (response is null)
        {
            throw new ApiException(
                HttpStatusCode.Unauthorized,
                "The email or password is incorrect.",
                "invalid_credentials");
        }

        await ApplySessionAsync(response, cancellationToken);
    }

    public async Task RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await apiClient.RegisterAsync(request, cancellationToken);
        await ApplySessionAsync(response, cancellationToken);
    }

    public async Task<bool> EnsureSignedInAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (await tokenStore.GetAccessTokenAsync(cancellationToken) is not null)
            {
                // Sessions stored before roles existed belong to the old user role,
                // which is now the student role.
                CurrentRole ??= await tokenStore.GetRoleAsync(cancellationToken) ?? AppUserRole.Student;
                SessionChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }

            var response = await apiClient.DemoLoginAsync(cancellationToken);
            if (response is null)
            {
                return false;
            }

            await ApplySessionAsync(response, cancellationToken);
            return true;
        }
        catch (Exception)
        {
            // Automatic sign-in is best effort; the app must still start when the API is unreachable.
            return false;
        }
    }

    public async Task<AppUserRole?> GetCurrentRoleAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentRole is { } role)
        {
            return role;
        }

        if (!await IsSignedInAsync(cancellationToken))
        {
            return null;
        }

        // Sessions stored before roles existed belong to the old user role,
        // which is now the student role.
        CurrentRole = await tokenStore.GetRoleAsync(cancellationToken) ?? AppUserRole.Student;
        return CurrentRole;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await apiClient.LogoutAsync(cancellationToken);
        }
        catch (Exception)
        {
            // A local sign-out must succeed even when the API cannot be reached.
        }
        finally
        {
            await tokenStore.ClearAsync(cancellationToken);
            CurrentRole = null;
            SessionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task<bool> IsSignedInAsync(CancellationToken cancellationToken = default)
        => await tokenStore.GetAccessTokenAsync(cancellationToken) is not null;

    private async Task ApplySessionAsync(LoginResponse response, CancellationToken cancellationToken)
    {
        var role = AppUserRoleWire.TryParse(response.Role) ?? AppUserRole.Student;
        await tokenStore.SaveAsync(response.AccessToken, response.ExpiresAt, role, cancellationToken);
        CurrentRole = role;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}
