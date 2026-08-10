using HousingHub.Service.Commons.Web;
using HousingHub.Service.Dtos.Property;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace HousingHub.Test.Commons;

public class PaginationClampFilterTests
{
    private readonly PaginationClampFilter _sut = new();

    // A record DTO with `init`-only properties, exactly like the real filter DTOs
    // (GetAllPropertiesFilterDto, AdminCustomerFilterDto, etc).
    private sealed record InitOnlyPagingArgs
    {
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 10;
        public string? Search { get; init; }
    }

    // No setter and no init accessor at all — forces the filter's reflection
    // fallback onto the compiler-generated backing field, the path the security
    // handover doc flagged as unverified.
    private sealed class GetOnlyPagingArgs(int pageNumber, int pageSize)
    {
        public int PageNumber { get; } = pageNumber;
        public int PageSize { get; } = pageSize;
    }

    private static ActionExecutingContext CreateContext(Dictionary<string, object?> actionArguments)
    {
        var actionContext = new Microsoft.AspNetCore.Mvc.ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            actionArguments,
            controller: new object());
    }

    // ── Scalar int arguments ─────────────────────────────────────

    [Fact]
    public void OnActionExecuting_ScalarPageSizeAboveMax_ClampsToMax()
    {
        var context = CreateContext(new() { ["pageSize"] = 100_000_000 });

        _sut.OnActionExecuting(context);

        Assert.Equal(PaginationClampFilter.MaxPageSize, context.ActionArguments["pageSize"]);
    }

    [Fact]
    public void OnActionExecuting_ScalarPageSizeZeroOrNegative_ClampsToOne()
    {
        var context = CreateContext(new() { ["pageSize"] = -5 });

        _sut.OnActionExecuting(context);

        Assert.Equal(1, context.ActionArguments["pageSize"]);
    }

    [Fact]
    public void OnActionExecuting_ScalarPageSizeWithinRange_IsUnchanged()
    {
        var context = CreateContext(new() { ["pageSize"] = 25 });

        _sut.OnActionExecuting(context);

        Assert.Equal(25, context.ActionArguments["pageSize"]);
    }

    [Fact]
    public void OnActionExecuting_ScalarNegativePageNumber_ClampsToOne()
    {
        // A negative page number used to reach Skip(negative) and produce an
        // unhandled 500 — this is the exact case that regression guards against.
        var context = CreateContext(new() { ["pageNumber"] = -3 });

        _sut.OnActionExecuting(context);

        Assert.Equal(1, context.ActionArguments["pageNumber"]);
    }

    [Fact]
    public void OnActionExecuting_ArgumentKeyMatchIsCaseInsensitive()
    {
        var context = CreateContext(new() { ["PageSize"] = 100_000_000 });

        _sut.OnActionExecuting(context);

        Assert.Equal(PaginationClampFilter.MaxPageSize, context.ActionArguments["PageSize"]);
    }

    [Fact]
    public void OnActionExecuting_UnrelatedIntArgument_IsUnchanged()
    {
        var context = CreateContext(new() { ["retryCount"] = 100_000_000 });

        _sut.OnActionExecuting(context);

        Assert.Equal(100_000_000, context.ActionArguments["retryCount"]);
    }

    // ── Filter objects with init-only properties (the real DTO shape) ───

    [Fact]
    public void OnActionExecuting_InitOnlyFilterObject_ClampsPageSizeInPlace()
    {
        var filter = new InitOnlyPagingArgs { PageNumber = 1, PageSize = 500_000 };
        var context = CreateContext(new() { ["filter"] = filter });

        _sut.OnActionExecuting(context);

        var clamped = Assert.IsType<InitOnlyPagingArgs>(context.ActionArguments["filter"]);
        Assert.Equal(PaginationClampFilter.MaxPageSize, clamped.PageSize);
    }

    [Fact]
    public void OnActionExecuting_InitOnlyFilterObject_ClampsPageNumberInPlace()
    {
        var filter = new InitOnlyPagingArgs { PageNumber = -1, PageSize = 10 };
        var context = CreateContext(new() { ["filter"] = filter });

        _sut.OnActionExecuting(context);

        var clamped = Assert.IsType<InitOnlyPagingArgs>(context.ActionArguments["filter"]);
        Assert.Equal(1, clamped.PageNumber);
    }

    [Fact]
    public void OnActionExecuting_InitOnlyFilterObject_LeavesNonPagingPropertiesAlone()
    {
        var filter = new InitOnlyPagingArgs { PageNumber = 1, PageSize = 10, Search = "lagos" };
        var context = CreateContext(new() { ["filter"] = filter });

        _sut.OnActionExecuting(context);

        var clamped = Assert.IsType<InitOnlyPagingArgs>(context.ActionArguments["filter"]);
        Assert.Equal("lagos", clamped.Search);
    }

    [Fact]
    public void OnActionExecuting_InitOnlyFilterObject_ValuesAlreadyInRange_AreUnchanged()
    {
        var filter = new InitOnlyPagingArgs { PageNumber = 3, PageSize = 20 };
        var context = CreateContext(new() { ["filter"] = filter });

        _sut.OnActionExecuting(context);

        var clamped = Assert.IsType<InitOnlyPagingArgs>(context.ActionArguments["filter"]);
        Assert.Equal(3, clamped.PageNumber);
        Assert.Equal(20, clamped.PageSize);
    }

    [Fact]
    public void OnActionExecuting_RealFilterDto_ClampsCorrectly()
    {
        // Exercise an actual production DTO, not just a look-alike.
        var filter = new GetMyPropertiesFilterDto { PageNumber = -1, PageSize = 999 };
        var context = CreateContext(new() { ["filter"] = filter });

        _sut.OnActionExecuting(context);

        var clamped = Assert.IsType<GetMyPropertiesFilterDto>(context.ActionArguments["filter"]);
        Assert.Equal(1, clamped.PageNumber);
        Assert.Equal(PaginationClampFilter.MaxPageSize, clamped.PageSize);
    }

    // ── Filter objects with no setter at all (backing-field fallback) ───

    [Fact]
    public void OnActionExecuting_GetOnlyFilterObject_ClampsViaBackingField()
    {
        var filter = new GetOnlyPagingArgs(pageNumber: 1, pageSize: 999_999);
        var context = CreateContext(new() { ["filter"] = filter });

        _sut.OnActionExecuting(context);

        var clamped = Assert.IsType<GetOnlyPagingArgs>(context.ActionArguments["filter"]);
        Assert.Equal(PaginationClampFilter.MaxPageSize, clamped.PageSize);
    }

    [Fact]
    public void OnActionExecuting_GetOnlyFilterObject_NegativePageNumber_ClampsViaBackingField()
    {
        var filter = new GetOnlyPagingArgs(pageNumber: -10, pageSize: 10);
        var context = CreateContext(new() { ["filter"] = filter });

        _sut.OnActionExecuting(context);

        var clamped = Assert.IsType<GetOnlyPagingArgs>(context.ActionArguments["filter"]);
        Assert.Equal(1, clamped.PageNumber);
    }

    // ── Robustness ───────────────────────────────────────────────

    [Fact]
    public void OnActionExecuting_NullArgumentValue_DoesNotThrow()
    {
        var context = CreateContext(new() { ["filter"] = null });

        var exception = Record.Exception(() => _sut.OnActionExecuting(context));

        Assert.Null(exception);
    }

    [Theory]
    [InlineData("just a string")]
    public void OnActionExecuting_SimpleStringArgument_DoesNotThrow(string value)
    {
        var context = CreateContext(new() { ["search"] = value });

        var exception = Record.Exception(() => _sut.OnActionExecuting(context));

        Assert.Null(exception);
        Assert.Equal(value, context.ActionArguments["search"]);
    }

    [Fact]
    public void OnActionExecuting_GuidArgument_DoesNotThrow()
    {
        var id = Guid.NewGuid();
        var context = CreateContext(new() { ["id"] = id });

        var exception = Record.Exception(() => _sut.OnActionExecuting(context));

        Assert.Null(exception);
        Assert.Equal(id, context.ActionArguments["id"]);
    }

    [Fact]
    public void OnActionExecuting_NoActionArguments_DoesNotThrow()
    {
        var context = CreateContext(new());

        var exception = Record.Exception(() => _sut.OnActionExecuting(context));

        Assert.Null(exception);
    }
}
