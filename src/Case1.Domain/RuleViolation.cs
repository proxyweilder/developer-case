namespace Case1.Domain;
public sealed class RuleViolation(int status, string code, string message, object? detail = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public object? Detail { get; } = detail;
}
