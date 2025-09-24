namespace HousingHub.Core.CustomResponses;

public class BaseResponse<T>(T? data, bool isSuccessful, string statusCode, string message)
{
    public bool IsSuccessful { get; set; } = isSuccessful;
    public string StatusCode { get; set; } = statusCode;
    public string Message { get; set; } = message;
    public T? Data { get; set; } = data;
}