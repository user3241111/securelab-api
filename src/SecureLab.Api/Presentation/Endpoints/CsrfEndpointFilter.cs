using Microsoft.AspNetCore.Antiforgery;

namespace SecureLab.Api.Presentation.Endpoints;

public static class CsrfEndpointFilter
{
    public static async ValueTask<object?> ValidateAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        var antiforgery = httpContext.RequestServices
            .GetRequiredService<IAntiforgery>();

        if (!await antiforgery.IsRequestValidAsync(httpContext))
        {
            return Results.Problem(
                title: "Invalid CSRF token",
                detail: "CSRF token is missing or invalid.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }
}
