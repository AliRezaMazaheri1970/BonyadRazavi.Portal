using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BonyadRazavi.Auth.Api.Observability;

public sealed class ProblemDetailsCorrelationFilter : IAsyncResultFilter
{
    public Task OnResultExecutionAsync(
        ResultExecutingContext context,
        ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult { Value: ProblemDetails problemDetails })
        {
            problemDetails.Extensions["correlationId"] =
                CorrelationIdMiddleware.GetCorrelationId(context.HttpContext);
        }

        return next();
    }
}
