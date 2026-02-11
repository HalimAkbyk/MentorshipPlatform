namespace MentorshipPlatform.Application.Common.Models;

public class Result
{
    public bool IsSuccess { get; }
    public string[] Errors { get; }

    protected Result(bool isSuccess, string[] errors)
    {
        IsSuccess = isSuccess;
        Errors = errors;
    }

    public static Result Success() => new(true, Array.Empty<string>());
    public static Result Failure(params string[] errors) => new(false, errors);
}
public class Result<T> : Result
{
    public T? Data { get; }

    private Result(bool isSuccess, T? data, string[] errors) : base(isSuccess, errors)
    {
        Data = data;
    }

    public static Result<T> Success(T data) => new(true, data, Array.Empty<string>());
    public static new Result<T> Failure(params string[] errors) => new(false, default, errors);
}