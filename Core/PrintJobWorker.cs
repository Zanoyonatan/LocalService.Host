using LocalService.Host.Abstractions;
using LocalService.Host.Printing;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace LocalService.Host.Core;

public sealed class PrintWorker
{
    private readonly BlockingCollection<PrintJob> _queue;
    private readonly IPrinterService _printer;
    private readonly ILogger<PrintWorker> _logger;

    public PrintWorker(
        IPrinterService printer,
        ILogger<PrintWorker> logger)
    {
        _queue = new BlockingCollection<PrintJob>();
        _printer = printer;
        _logger = logger;

        Task.Run(WorkLoop);
    }

    public void Enqueue(PrintJob job) => _queue.Add(job);

    private async Task WorkLoop()
    {
        foreach (var job in _queue.GetConsumingEnumerable())
        {
            try
            {
                _logger.LogInformation("Processing print job: DocumentType={DocumentType}, Printer={Printer}, Tray={Tray}",
                    job.DocumentType, job.PrinterName, job.Tray);

                var result = await _printer.PrintPdfBase64Async(job.DocumentType, job.Base64, job.PrinterName, job.Tray);

                if (result.Success)
                {
                    _logger.LogInformation("Print job completed: Printer={Printer}", job.PrinterName);
                    job.Completion.TrySetResult(result);
                }
                else
                {
                    _logger.LogError("Print job failed: Printer={Printer}, Error={Error}", job.PrinterName, result.Error);
                    job.Completion.TrySetResult(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Print job failed: Printer={Printer}", job.PrinterName);
                job.Completion.TrySetResult(PrintSubmitResult.Fail(ex.Message));
            }
        }
    }
}
