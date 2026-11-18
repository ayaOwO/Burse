using Microsoft.AspNetCore.Diagnostics;
using Common.Exceptions;

namespace Presentation.ExceptionHandlers;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        int? status = exception switch
        {
            KeyNotFoundException => StatusCodes.Status404NotFound,
            ArgumentException => StatusCodes.Status400BadRequest,
            InsufficientFundsException or InsufficientStocksException => StatusCodes.Status409Conflict,
            InvalidOperationException => StatusCodes.Status409Conflict,
            _ => null
        };
        if (status is null)
        { 
            return false;
        }

        await Results.Problem(statusCode: status, title: exception.Message).ExecuteAsync(httpContext);
        return true;
    }
}
