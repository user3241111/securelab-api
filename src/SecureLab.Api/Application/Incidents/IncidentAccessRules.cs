
using System.Security.Claims;

namespace SecureLab.Api.Application.Incidents;

public static class IncidentAccessRules
{
    public static Guid? GetCurrentUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(value, out var userId)
            ? userId
            : null;
    }

    public static bool IsReporter(ClaimsPrincipal user)
    {
        return user.IsInRole("Reporter");
    }

    public static bool IsAnalyst(ClaimsPrincipal user)
    {
        return user.IsInRole("Analyst");
    }

    public static bool CanReadList(ClaimsPrincipal user)
    {
        return IsReporter(user) || IsAnalyst(user);
    }

    public static bool CanRead(
        ClaimsPrincipal user,
        Guid ownerUserId)
    {
        if (IsAnalyst(user))
        {
            return true;
        }

        var currentUserId = GetCurrentUserId(user);

        return IsReporter(user)
            && currentUserId.HasValue
            && currentUserId.Value == ownerUserId;
    }

    public static bool CanUpdate(
        ClaimsPrincipal user,
        Guid ownerUserId)
    {
        var currentUserId = GetCurrentUserId(user);

        return IsReporter(user)
            && currentUserId.HasValue
            && currentUserId.Value == ownerUserId;
    }

    public static bool CanDelete(
        ClaimsPrincipal user,
        Guid ownerUserId)
    {
        // Видалення не дозволене предметною матрицею.
        return false;
    }

    public static bool CanReadComment(
        ClaimsPrincipal user,
        Guid ownerUserId,
        bool isInternal)
    {
        if (!CanRead(user, ownerUserId))
        {
            return false;
        }

        return !isInternal || IsAnalyst(user);
    }

    public static bool CanTriage(ClaimsPrincipal user)
    {
        return IsAnalyst(user);
    }
}
