using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RoomToRentRadar.Api.Services;

namespace RoomToRentRadar.Api.Filters;

/// <summary>
/// Đổi <see cref="RequestValidationException"/> thành HTTP 400 <see cref="ValidationProblemDetails"/> một chỗ,
/// để controller không phải lặp try/catch (cùng format với <c>ValidationProblem(ModelState)</c>).
/// </summary>
public sealed class RequestValidationExceptionFilter(ProblemDetailsFactory problemDetailsFactory) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not RequestValidationException ex)
        {
            return;
        }

        var modelState = new ModelStateDictionary();
        foreach (var (field, messages) in ex.Errors)
        {
            foreach (var message in messages)
            {
                modelState.AddModelError(field, message);
            }
        }

        var problem = problemDetailsFactory.CreateValidationProblemDetails(
            context.HttpContext, modelState, StatusCodes.Status400BadRequest);
        context.Result = new BadRequestObjectResult(problem);
        context.ExceptionHandled = true;
    }
}
