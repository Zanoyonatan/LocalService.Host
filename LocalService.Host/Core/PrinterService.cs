
using LocalService.Host.Infra;
using PdfiumViewer;
using System.Collections.Concurrent;
using System.Drawing.Printing;
namespace LocalService.Host.Core;
sealed class PrintData
{
    public required string DocumentType { get; set; }
    public required byte[] Base64bytes { get; set; }
    public required string PrinterName { get; set; }
    public required string Tray { get; set; }
}

public sealed class PrinterService : IPrinterService
{
    private readonly ILogger<PrinterService> _logger;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _printerLocks = new();

    private static SemaphoreSlim GetPrinterLock(string printerName)
    {
        return _printerLocks.GetOrAdd(printerName, _ => new SemaphoreSlim(1, 1));
    }
    public PrinterService(ILogger<PrinterService> logger)
    {
        _logger = logger;
    }

    public async Task<PrintSubmitResult> PrintPdfBase64Async(string documentType, string base64, string printerName, string tray)
    {

        PrintData printdata = new PrintData() { DocumentType = documentType, Base64bytes = Convert.FromBase64String(base64), PrinterName = printerName, Tray = tray };
        try
        {
            var filePath = await PrepareTempFile(printdata);
            
            //lock by printerName
            var sem = GetPrinterLock(printdata.PrinterName);
            await sem.WaitAsync();

            try
            {
                PrintWithTray(printdata.PrinterName, printdata.Tray,filePath);
            }
            finally
            {
                sem.Release();
            }

            return PrintSubmitResult.Ok();
        
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Print failed");
            return PrintSubmitResult.Fail(ex.Message);
        }
      
    }
    private async Task<string> PrepareTempFile(PrintData printdata)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LocalServicePrint");
        Directory.CreateDirectory(tempDir);

        var filePath = Path.Combine(tempDir, $"{printdata.DocumentType}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.pdf");
        await File.WriteAllBytesAsync(filePath, printdata.Base64bytes);
        _logger.LogInformation ("---------------------------------------");
        _logger.LogInformation("Printing: DocType={DocType}, Printer={Printer}, Tray={Tray}, File={File}",
            printdata.DocumentType, printdata.PrinterName, printdata.Tray, filePath);
        _logger.LogInformation("---------------------------------------");

        return filePath;
    }

    public static void PrintWithTray(
        string printerName,
        string trayName,
        string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new ArgumentException("Printer name is required");

        if (string.IsNullOrWhiteSpace(trayName))
            throw new ArgumentException("Tray name is required");

        if (!File.Exists(pdfPath))
            throw new FileNotFoundException("PDF file not found", pdfPath);

        using var pdf = PdfDocument.Load(pdfPath);

        using var printDoc = pdf.CreatePrintDocument();

        printDoc.PrinterSettings.PrinterName = printerName;

        if (!printDoc.PrinterSettings.IsValid)
            throw new InvalidOperationException(
                $"Printer '{printerName}' is not valid");

        bool isPrinterHasTraies = printDoc.PrinterSettings.PaperSources?.Count > 0;
        if (isPrinterHasTraies)
        {
            var trayKind = TrayKindMapper.Parse(trayName);
            var tray = printDoc.PrinterSettings.PaperSources
                .Cast<PaperSource>()
                 .FirstOrDefault(ps => ps.Kind == trayKind);

            if (tray == null)
            {
                var available = string.Join(", ",
                    printDoc.PrinterSettings.PaperSources
                        .Cast<PaperSource>()
                        .Select(p => p.SourceName));

                throw new InvalidOperationException(
                    $"Tray '{trayName}' not found. Available trays: {available}");
            }

            //setting the tray for printing
            printDoc.DefaultPageSettings.PaperSource = tray;
        }

        // הדפס
        printDoc.Print();
    }
}





