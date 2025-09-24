namespace HousingHub.Application.Commons.Bases;

public class BaseReponseGeneric<T>(bool isSuccessful, T? data, string? message, IEnumerable<BaseError>? errors)
{
    public bool IsSucccessful { get; set; } = isSuccessful;
    public T? Data { get; set; } = data;
    public string? Message { get; set; } = message;
    public IEnumerable<BaseError>? Errors { get; set; } = errors;
}