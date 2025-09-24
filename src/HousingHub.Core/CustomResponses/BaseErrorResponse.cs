namespace HousingHub.Core.CustomResponses;

public class BaseErrorResponse
{
    public BaseErrorResponse(HashSet<string> error, string statusCode, string message)
    {
        StatusCode = statusCode;
        Message = message;
        Error = error;
    }
    public string StatusCode { get; set; }
    public string Message { get; set; }
    public HashSet<string> Error { get; set; }
}
