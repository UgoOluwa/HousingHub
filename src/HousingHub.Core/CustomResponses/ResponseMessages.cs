namespace HousingHub.Core.CustomResponses;

public static class ResponseMessages
{
    public const string Successful = "Operation Successful";
    public const string Failed = "Operation Failed";
    public static string SetCreationSuccessMessage(string message)
    {
        var successMessage = $"Successfuly created {message}";
        return successMessage;
    }

    public static string SetCreationFailureMessage(string message)
    {
        var successMessage = $"Failed to create {message}";
        return successMessage;
    }

    public static string SetNotFoundMessage(string message)
    {
        var successMessage = $"{message} Not Found";
        return successMessage;
    }
} 
