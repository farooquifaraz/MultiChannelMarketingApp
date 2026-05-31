using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MarketingApp.Application.DTOs;
using MarketingApp.Domain.Exceptions;

namespace MarketingApp.API.Filters;

public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
        => _logger = logger;

    public void OnException(ExceptionContext context)
    {
        var correlationId = context.HttpContext.Items["CorrelationId"]?.ToString();

        var (statusCode, message) = context.Exception switch
        {
            AppValidationException ex => (400, ex.Message),
            NotFoundException ex => (404, ex.Message),
            ForbiddenException ex => (403, ex.Message),
            ConflictException ex => (409, ex.Message),
            ExternalServiceException => (502, "External service error. Please try again."),
            UnauthorizedAccessException => (401, "Unauthorized access."),
            _ when IsForeignKeyViolation(context.Exception) =>
                (409, "Can't delete this item because other records still depend on it (for example, campaigns that use this group or template). Delete or reassign those first."),
            _ => (500, "An unexpected error occurred.")
        };

        _logger.LogError(context.Exception,
            "Unhandled exception. CorrelationId: {CorrelationId} | Path: {Path} | StatusCode: {StatusCode}",
            correlationId, context.HttpContext.Request.Path, statusCode);

        var response = new ApiResponse
        {
            Success = false,
            Message = message,
            CorrelationId = correlationId,
        };

        if (context.Exception is AppValidationException vex)
            response.Errors = vex.Errors.ToList();

        context.Result = new ObjectResult(response) { StatusCode = statusCode };
        context.ExceptionHandled = true;
    }

    /// <summary>Detects a PostgreSQL foreign-key violation (SQLSTATE 23503) anywhere in the exception
    /// chain, without taking a hard Npgsql dependency. Surfaced as a friendly 409 instead of a 500 so
    /// RESTRICT-protected deletes (e.g. deleting a ContactGroup/Template still used by campaigns) give
    /// the user a clear, actionable message.</summary>
    private static bool IsForeignKeyViolation(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            var sqlState = e.GetType().GetProperty("SqlState")?.GetValue(e) as string;
            if (sqlState == "23503") return true;
            if (e.Message.Contains("23503") ||
                e.Message.Contains("foreign key", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
