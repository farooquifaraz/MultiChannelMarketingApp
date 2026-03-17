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
}
