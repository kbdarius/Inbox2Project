namespace Inbox2Project.Models;

public sealed record OperationResult(
    bool Succeeded,
    string UserMessage,
    IReadOnlyList<string> OutputPaths,
    AppErrorId? ErrorId = null,
    string? TechnicalMessage = null)
{
    public static OperationResult Success(string message, params string[] outputPaths) =>
        new(true, message, outputPaths);

    public static OperationResult Failure(AppErrorId errorId, string userMessage, string technicalMessage) =>
        new(false, userMessage, Array.Empty<string>(), errorId, technicalMessage);
}