using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace RoomToRentRadar.Api.Controllers;

public static class ControllerIdentityHelper
{
    public const string UserIdHeader = "X-User-Id";
    public const string UserRoleHeader = "X-User-Role";

    public static bool TryResolveUserId(
        ControllerBase controller,
        out string userId,
        out ActionResult? errorResult)
    {
        var claimId = controller.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? controller.User?.FindFirstValue("sub");
        var headerId = controller.Request?.Headers[UserIdHeader].FirstOrDefault();
        var raw = !string.IsNullOrWhiteSpace(claimId) ? claimId : headerId;

        if (string.IsNullOrWhiteSpace(raw) || raw.Trim().Length > 100)
        {
            userId = string.Empty;
            errorResult = controller.Problem(
                detail: $"Thiếu thông tin người dùng (cần JWT claim hoặc header '{UserIdHeader}' <= 100 ký tự).",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Chưa xác thực người dùng");
            return false;
        }

        userId = raw.Trim();
        errorResult = null;
        return true;
    }

    public static bool TryResolveModerator(
        ControllerBase controller,
        out string moderatorId,
        out ActionResult? errorResult)
    {
        if (!TryResolveUserId(controller, out moderatorId, out errorResult))
        {
            return false;
        }

        bool hasModeratorRole = controller.User?.IsInRole("Moderator") == true
            || controller.User?.IsInRole("Admin") == true;

        if (!hasModeratorRole)
        {
            var headerRole = controller.Request?.Headers[UserRoleHeader].FirstOrDefault()?.Trim();
            hasModeratorRole = string.Equals(headerRole, "Moderator", StringComparison.OrdinalIgnoreCase)
                || string.Equals(headerRole, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        if (!hasModeratorRole)
        {
            errorResult = controller.Problem(
                detail: $"Chỉ tài khoản vai trò Moderator/Admin (JWT role hoặc header '{UserRoleHeader}: Moderator') mới được thao tác kiểm duyệt.",
                statusCode: StatusCodes.Status403Forbidden,
                title: "Không có quyền kiểm duyệt");
            return false;
        }

        errorResult = null;
        return true;
    }
}
