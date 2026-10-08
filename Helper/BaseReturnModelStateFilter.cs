using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ContactTogetherApi.Helper;

/// <summary>
/// Answers an invalid model state (malformed JSON, missing body) with a 400 <see cref="_0BaseReturn"/>
/// instead of the <c>ProblemDetails</c> that <c>[ApiController]</c> sends. Apply it per controller;
/// changing <c>InvalidModelStateResponseFactory</c> would also change <c>ValidationProblem()</c>
/// in the controllers that use ProblemDetails.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class BaseReturnModelStateFilterAttribute : ActionFilterAttribute
{
    // ModelStateInvalidFilter runs at -2000; run first so it never sees the invalid state.
    public BaseReturnModelStateFilterAttribute() => Order = -2001;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            context.Result = new BadRequestObjectResult(_0BaseReturn.Fail("รูปแบบข้อมูลที่ส่งมาไม่ถูกต้อง"));
        }
    }
}
