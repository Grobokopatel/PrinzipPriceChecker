using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace PrinzipPriceChecker.Tests;

internal static class AssertionExtensions
{
    public static TValue IsOk<TValue>(this ActionResult<TValue> result) 
        => result.Result.OfType<OkObjectResult>().Value.OfType<TValue>();

    public static TValue IsCreated<TValue>(this ActionResult<TValue> result)
        => result.Result.OfType<CreatedAtActionResult>().Value.OfType<TValue>();

    public static ValidationProblemDetails IsValidationProblem<TValue>(this ActionResult<TValue> result)
        => result.Result.OfType<BadRequestObjectResult>().Value.OfType<ValidationProblemDetails>();
    
    private static T OfType<T>(this object? value)
    {
        Assert.That(value, Is.TypeOf<T>());

        return (T)value!;
    }
}
