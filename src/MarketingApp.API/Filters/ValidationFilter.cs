using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MarketingApp.Application.DTOs;

namespace MarketingApp.API.Filters;

public class ValidationFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();

            context.Result = new BadRequestObjectResult(new ApiResponse
            {
                Success = false,
                Message = "Validation failed",
                Errors = errors
            });
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
