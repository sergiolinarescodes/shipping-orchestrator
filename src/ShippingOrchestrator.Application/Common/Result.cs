using OneOf;

namespace ShippingOrchestrator.Application.Common;

public sealed record AppError(string Code, string Message)
{
    public static AppError NotFound(string what) => new("not_found", $"{what} was not found.");
    public static AppError Validation(string message) => new("validation", message);
    public static AppError Conflict(string message) => new("conflict", message);
    public static AppError External(string message) => new("external", message);
}

public abstract class Result<TValue> : OneOfBase<TValue, AppError>
{
    protected Result(OneOf<TValue, AppError> input) : base(input) { }
}
