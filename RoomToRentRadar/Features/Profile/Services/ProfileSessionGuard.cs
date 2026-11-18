using RoomToRentRadar.Core.Interfaces;
using RoomToRentRadar.Core.Models.Authentication;
using RoomToRentRadar.Features.Profile.Models;

namespace RoomToRentRadar.Features.Profile.Services;

internal static class ProfileSessionGuard
{
    public static bool IsCurrentSession(
        IAuthenticationSessionService sessionService,
        AuthUser expectedUser) =>
        sessionService.IsAuthenticated &&
        sessionService.CurrentUser?.Id == expectedUser.Id;

    public static bool CanApplyProfile(
        IAuthenticationSessionService sessionService,
        AuthUser expectedUser,
        ProfileApiDto profile) =>
        IsCurrentSession(sessionService, expectedUser) &&
        profile.UserId == expectedUser.Id;
}
