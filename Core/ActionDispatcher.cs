using LocalService.Host.Abstractions;
using LocalService.Host.Printing;
using Microsoft.Extensions.Logging;

namespace LocalService.Host.Core;

public sealed class ActionDispatcher
{
    private readonly ILogger<ActionDispatcher> _logger;
  
    private readonly PrintWorker _worker;

    public ActionDispatcher(
        ILogger<ActionDispatcher> logger,
     
        PrintWorker worker)
    {
        _logger = logger;
        _worker = worker;
    }

    public async Task<ExecuteResult> ExecuteAsync(ExecuteRequest req)
    {
        if (req is null)
            return ExecuteResult.Fail(400, "bad_request");

        if (string.IsNullOrWhiteSpace(req.ActionType))
            return ExecuteResult.Fail(400, "missing_actionType");

        _logger.LogInformation("Execute: ActionType={ActionType}", req.ActionType);

        if (req.ActionType.Equals("Print", StringComparison.OrdinalIgnoreCase))
            return await HandlePrintAsync(req);

        return ExecuteResult.Fail(400, $"unknown_actionType:{req.ActionType}");
    }

    private async Task<ExecuteResult> HandlePrintAsync(ExecuteRequest req)
    {
        // expected parameters: documentType, fileBase64
        if (!req.Parameters.TryGetValue("documentType", out var docTypeObj) || docTypeObj is null)
            return ExecuteResult.Fail(400, "missing_documentType");

        if (!req.Parameters.TryGetValue("fileBase64", out var base64Obj) || base64Obj is null)
            return ExecuteResult.Fail(400, "missing_fileBase64");

        var documentType = docTypeObj.ToString() ?? "";
        var fileBase64 = base64Obj.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(documentType))
            return ExecuteResult.Fail(400, "invalid_documentType");

        if (string.IsNullOrWhiteSpace(fileBase64))
            return ExecuteResult.Fail(400, "invalid_fileBase64");

        try
        {
            var job = new PrintJob
            {
                DocumentType = documentType,
                Base64 = fileBase64
            };

            _worker.Enqueue(job);

            var timeout = TimeSpan.FromSeconds(20);
            var delayTask = Task.Delay(timeout);
            var completed = await Task.WhenAny(job.Completion.Task, delayTask);

            if (completed == job.Completion.Task)
            {
                // print result finished within timeout
                var result = await job.Completion.Task; // already completed
                if (result.Success)
                    return ExecuteResult.Accepted(message: "print_accepted");

                return result.Error switch
                {
                    "printer_not_found" => ExecuteResult.Fail(400, "printer_not_found"),
                    "invalid_tray" => ExecuteResult.Fail(400, "invalid_tray"),
                    "file_not_valid" => ExecuteResult.Fail(400, "file_not_valid"),

                    _ => ExecuteResult.Fail(500, $"print_failed:{result.Error}")
                };
            }
            else
            {
                // timeout expired, job continues in background
                _logger.LogWarning("Print request timeout for DocumentType={DocumentType}. Job continues in background.", documentType);
                return ExecuteResult.Fail(504, "print_timeout");

            }
        }
        catch (FormatException)
        {
            return ExecuteResult.Fail(400, "fileBase64_not_valid_base64");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Print enqueue failed");
            return ExecuteResult.Fail(500, "print_failed_exception");
        }
    }
}
