using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Infrastructure.Api;
using RoomToRentRadar.Shared.Auth;

namespace RoomToRentRadar.Infrastructure.Auth;

public sealed class AuthService(IApiClient apiClient, ITokenStore tokenStore)
{
    public async Task<bool> SignInAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var response = await apiClient.LoginAsync(new LoginRequest(email, password), cancellationToken);
        if (response is null)
        {
            return false;
        }

        await tokenStore.SaveAsync(response.AccessToken, response.ExpiresAt, cancellationToken);
        return true;
    }

    public async Task<bool> EnsureSignedInAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (await tokenStore.GetAccessTokenAsync(cancellationToken) is not null)
            {
                return true;
            }

            var response = await apiClient.DemoLoginAsync(cancellationToken);
            if (response is null)
            {
                return false;
            }

            await tokenStore.SaveAsync(response.AccessToken, response.ExpiresAt, cancellationToken);
            return true;
        }
        catch (Exception)
        {
            // Automatic sign-in is best effort; the app must still start when the API is unreachable.
            return false;
        }
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
        }
    }

    public async Task<bool> IsSignedInAsync(CancellationToken cancellationToken = default)
        => await tokenStore.GetAccessTokenAsync(cancellationToken) is not null;
}
