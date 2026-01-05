namespace HousingHub.Application.Commons.Bases;

public class BaseResponsePagination<T>(bool isSuccessful, T? data, string? message, IEnumerable<BaseError>? errors) : BaseResponseGeneric<T>(isSuccessful, data, message, errors)
{
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
