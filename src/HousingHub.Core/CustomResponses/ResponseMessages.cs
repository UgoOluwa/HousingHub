namespace HousingHub.Core.CustomResponses;

public static class ResponseMessages
{
    public const string Successful = "Operation Successful";
    public const string Failed = "Operation Failed";
    public const string CustomerAlreadyExists = "Customer with the same email or phone number already exists.";
    public static string SetCreationSuccessMessage(string message)
    {
        string successMessage = $"Successfully created {message}";
        return successMessage;
    }

    public static string SetUpdateSuccessMessage(string message)
    {
        string successMessage = $"Successfully updated {message}";
        return successMessage;
    }

    public static string SetDeletedSuccessMessage(string message)
    {
        string successMessage = $"Successfully deleted {message}";
        return successMessage;
    }

    public static string SetCreationFailureMessage(string message)
    {
        string failureMessage = $"Failed to create {message}";
        return failureMessage;
    }

    public static string SetUpdateFailureMessage(string message)
    {
        string failureMessage = $"Failed to update {message}";
        return failureMessage;
    }

    public static string SetDeletedFailureMessage(string message)
    {
        string failureMessage = $"Failed to deleted {message}";
        return failureMessage;
    }

    public static string SetNotFoundMessage(string message)
    {
        string notFoundMessage = $"{message} Not Found";
        return notFoundMessage;
    }

    public static string SetAlreadyExistsMessage(string message)
    {
        string alreadyExistsMessage = $"The {message} already exists.";
        return alreadyExistsMessage;
    }
} 
