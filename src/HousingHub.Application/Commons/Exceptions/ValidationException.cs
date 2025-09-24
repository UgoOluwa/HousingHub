using HousingHub.Application.Commons.Bases;

namespace HousingHub.Application.Commons.Exceptions;

public class ValidationException : Exception
{
    public IEnumerable<BaseError> Errors { get; }

    public ValidationException()
        : base("One or more validation failures have occured.")
    {
        Errors = new List<BaseError>();
    }

    public ValidationException(IEnumerable<BaseError> errors)
        : this()
    {
        Errors = errors;
    }
}
