using LocalService.Host.Models;

namespace LocalService.Host.Core;

public sealed class ActionDispatcher
{
    private readonly ILogger<ActionDispatcher> _logger;
    private readonly PrinterConfigStore _config;
    private readonly IPrinterService _printer;
    private readonly PrintWorker _worker;

    public ActionDispatcher(
        ILogger<ActionDispatcher> logger,
        PrinterConfigStore config,
        IPrinterService printer,
        PrintWorker worker)
    {
        _logger = logger;
        _config = config;
        _printer = printer;
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

        var mapping = _config.TryGetMapping(documentType);
        if (mapping is null)
            return ExecuteResult.Fail(404, $"no_printer_mapping_for:{documentType}");

        try
        {
            var job = new PrintJob
            {
                DocumentType = documentType,
                Base64 = fileBase64,
                PrinterName = mapping.PrinterName,
                Tray = mapping.Tray
            };

            _worker.Enqueue(job);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                var result = await job.Completion.Task.WaitAsync(cts.Token);

                return result.Success
                    ? ExecuteResult.Accepted(message: "print_accepted")
                    : ExecuteResult.Fail(500, $"print_failed:{result.Error}");
            }
            catch (OperationCanceledException)
            {
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
