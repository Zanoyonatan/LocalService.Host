using LocalService.Host.Abstractions;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace LocalService.Host.Printing;

public sealed class PrintWorker
{
    private readonly BlockingCollection<PrintJob> _queue;
    private readonly PrinterConfigStore _config;
    private readonly IPrinterService _printer;
    private readonly ILogger<PrintWorker> _logger;

    public PrintWorker(
        PrinterConfigStore config,
        IPrinterService printer,
        ILogger<PrintWorker> logger)
    {
        _queue = new BlockingCollection<PrintJob>();
        _config= config;
        _printer = printer;
        _logger = logger;

        Task.Run(WorkLoop);
    }

    public void Enqueue(PrintJob job) => _queue.Add(job);

    private async Task WorkLoop()
    {
        foreach (var job in _queue.GetConsumingEnumerable())
        {
           var mapping = _config.TryGetMapping(job.DocumentType);
            try
            {
                if (mapping is null)
                {
                    job.Completion.TrySetResult(PrintSubmitResult.Fail("no_document_type_for_printer_mapping"));
                    return;
                }

                if (string.IsNullOrWhiteSpace( mapping.PrinterName ))
                {
                    job.Completion.TrySetResult(PrintSubmitResult.Fail($"printer_not_configure_to_Doc_{job.DocumentType.ToLower()}"));
                    return;
                }

  
                _logger.LogInformation("Processing print job: DocumentType={DocumentType}, Printer={Printer}, Tray={Tray}",
                    job.DocumentType, mapping.PrinterName, mapping.Tray);

                var result = await _printer.PrintPdfBase64Async(job.DocumentType, job.Base64, mapping.PrinterName, mapping.Tray);

                if (result.Success)
                {
                    _logger.LogInformation("Print job completed: Printer={Printer}", mapping.PrinterName);
                    job.Completion.TrySetResult(result);
                }
                else
                {
                    _logger.LogError("Print job failed: Printer={Printer}, Error={Error}", mapping.PrinterName, result.Error);
                    job.Completion.TrySetResult(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Print job failed: Printer={Printer}", mapping?.PrinterName);
                job.Completion.TrySetResult(PrintSubmitResult.Fail(ex.Message));
            }
        }
    }
}
