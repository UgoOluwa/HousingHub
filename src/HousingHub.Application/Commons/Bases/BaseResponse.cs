namespace HousingHub.Application.Commons.Bases;

public class BaseResponse<T>(bool isSuccessful, T? data, string? message, IEnumerable<BaseError>? errors) : BaseResponseGeneric<T>(isSuccessful, data, message, errors)
{
}