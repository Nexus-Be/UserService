namespace User.Tests;

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using User.Api.Common;
using Xunit;

public sealed class ApiValidationProblemFactoryTests
{
    [Fact]
    public void Create_Returns_Problem_With_Validation_Error_Code()
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        actionContext.ModelState.AddModelError("RefreshToken", "The RefreshToken field is required.");

        var result = ApiValidationProblemFactory.Create(actionContext);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        Assert.Contains("application/problem+json", objectResult.ContentTypes);

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(objectResult.Value));
        var root = document.RootElement;
        Assert.Equal("VAL_INVALID_INPUT", root.GetProperty("errorCode").GetString());
        Assert.Equal(StatusCodes.Status400BadRequest, root.GetProperty("status").GetInt32());
        Assert.Contains("The RefreshToken field is required.", root.GetProperty("detail").GetString());
    }

    [Fact]
    public void Create_Falls_Back_To_Generic_Detail_When_ModelState_Is_Empty()
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        var result = ApiValidationProblemFactory.Create(actionContext);

        var objectResult = Assert.IsType<ObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(objectResult.Value));
        Assert.Equal(
            ApiValidationProblemFactory.GenericDetail,
            document.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public void Create_Hides_Internal_Type_Names_From_Json_Binding_Errors()
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        actionContext.ModelState.AddModelError(
            "$",
            "The JSON value could not be converted to User.Application.Auth.RegisterDto. Path: $ | LineNumber: 0 | BytePositionInLine: 1.");
        actionContext.ModelState.AddModelError("request", "The request field is required.");

        var result = ApiValidationProblemFactory.Create(actionContext);

        var objectResult = Assert.IsType<ObjectResult>(result);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(objectResult.Value));
        var detail = document.RootElement.GetProperty("detail").GetString();
        Assert.Equal(ApiValidationProblemFactory.UnreadableBodyDetail, detail);
        Assert.DoesNotContain("User.Application", detail);
        Assert.DoesNotContain("LineNumber", detail);
    }
}
