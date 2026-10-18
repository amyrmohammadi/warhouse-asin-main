namespace Warehouse.Application.Common.Models;

public class Result
{
    protected Result(bool isSuccess, IEnumerable<string>? messages)
    {
        IsSuccess = isSuccess;
        Messages = NormalizeMessages(messages);
    }

    public bool IsSuccess { get; }

    public List<string> Messages { get; }

    public static Result Success(params string[] messages) =>
        new(true, messages);

    public static Result Error(params string[] messages) =>
        new(false, messages);

    public static Result Error(IEnumerable<string> messages) =>
        new(false, messages);

    protected static List<string> NormalizeMessages(IEnumerable<string>? messages) =>
        messages?
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Select(message => message.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList() ?? [];
}

public sealed class Result<T> : Result
{
    private Result(bool isSuccess, T? data, IEnumerable<string>? messages)
        : base(isSuccess, messages)
    {
        Data = data;
    }

    public T? Data { get; }

    public static Result<T> Success(T data, params string[] messages) =>
        new(true, data, messages);

    public static new Result<T> Error(params string[] messages) =>
        new(false, default, messages);

    public static new Result<T> Error(IEnumerable<string> messages) =>
        new(false, default, messages);
}
