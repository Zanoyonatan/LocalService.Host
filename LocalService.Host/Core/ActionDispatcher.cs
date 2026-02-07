using LocalService.Host.Models;

namespace LocalService.Host.Core;

public sealed class ActionDispatcher
{
    private readonly ILogger<ActionDispatcher> _logger;
    private readonly PrinterConfigStore _config;
    private readonly IPrinterService _printer;

    public ActionDispatcher(
        ILogger<ActionDispatcher> logger,
        PrinterConfigStore config,
        IPrinterService printer)
    {
        _logger = logger;
        _config = config;
        _printer = printer;
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
            var printResult = await _printer.PrintPdfBase64Async(
                documentType: documentType,
                base64: fileBase64,
                printerName: mapping.PrinterName,
                tray: mapping.Tray);

            return printResult.Success
                ? ExecuteResult.Ok(message: "print_submitted")
                : ExecuteResult.Fail(500, $"print_failed:{printResult.Error}");
        }
        catch (FormatException)
        {
            return ExecuteResult.Fail(400, "fileBase64_not_valid_base64");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Print failed");
            return ExecuteResult.Fail(500, "print_failed_exception");
        }
    }
}
