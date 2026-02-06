using LocalService.Host.Infra;
using LocalService.Host.Infra.Printing;
using Microsoft.Win32.SafeHandles;
using PdfiumViewer;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
//using PdfiumViewer;

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

    // Generic print method: writes a temp file and dispatches to PDF/image printers
    public async Task<PrintSubmitResult> PrintBase64Async(string documentType, string base64, string printerName, string tray, string contentType)
    {
        var bytes = Convert.FromBase64String(base64);
        var printdata = new PrintData { DocumentType = documentType, Base64bytes = bytes, PrinterName = printerName, Tray = tray };

        try
        {
            string ext = contentType?.ToLowerInvariant() switch
            {
                "application/pdf" => "pdf",
                "image/png" => "png",
                "image/jpeg" => "jpg",
                "image/jpg" => "jpg",
                "image/bmp" => "bmp",
                _ => throw new InvalidOperationException($"Unsupported contentType: {contentType}")
            };

            var filePath = await PrepareTempFile(printdata, ext);

            var sem = GetPrinterLock(printdata.PrinterName);
            await sem.WaitAsync();
            try
            {
                if (ext == "pdf")
                {
                    PrintWithTray(printdata.PrinterName, printdata.Tray, filePath);
                }
                else
                {
                    PrintImageWithTrayFromFile(printdata.PrinterName, printdata.Tray, filePath);
                }
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

    private IDisposable PreparePrinterScope(PrintData printdata)
    {
        // placeholder for dev-mode or Win32 devmode scope
        return new DevModeScope(printdata.PrinterName, printdata.Tray);
    }

    private sealed class NoOpScope : IDisposable
    {
        public static readonly NoOpScope Instance = new();
        public void Dispose() { }
    }

    // Writes provided bytes to a temporary file with the given extension and returns the path
    private async Task<string> PrepareTempFile(PrintData printdata, string? extension = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LocalServicePrint");
        Directory.CreateDirectory(tempDir);

        var ext = string.IsNullOrWhiteSpace(extension) ? "pdf" : extension.TrimStart('.');
        var filePath = Path.Combine(tempDir, $"{printdata.DocumentType}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.{ext}");
        await File.WriteAllBytesAsync(filePath, printdata.Base64bytes);

        _logger.LogInformation("---------------------------------------");
        _logger.LogInformation("Printing: DocType={DocType}, Printer={Printer}, Tray={Tray}, File={File}",
            printdata.DocumentType, printdata.PrinterName, printdata.Tray, filePath);
        _logger.LogInformation("---------------------------------------");

        return filePath;
    }

    public static void PrintWithTray(string printerName, string trayName, string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new ArgumentException("Printer name is required", nameof(printerName));

        if (string.IsNullOrWhiteSpace(trayName))
            throw new ArgumentException("Tray name is required", nameof(trayName));

        if (!File.Exists(pdfPath))
            throw new FileNotFoundException("PDF file not found", pdfPath);

        using var pdf = PdfDocument.Load(pdfPath);
        using var printDoc = pdf.CreatePrintDocument();

        ConfigureAndValidatePrintDocument(printDoc, printerName, trayName);

        printDoc.Print();
    }

    public static void PrintImageWithTrayFromFile(string printerName, string trayName, string imageFile)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            throw new ArgumentException("Printer name is required", nameof(printerName));
        if (string.IsNullOrWhiteSpace(trayName))
            throw new ArgumentException("Tray name is required", nameof(trayName));
        if (!File.Exists(imageFile))
            throw new FileNotFoundException("Image file not found", imageFile);

        using var image = Image.FromFile(imageFile);
        using var printDoc = new PrintDocument();

        ConfigureAndValidatePrintDocument(printDoc, printerName, trayName);

        printDoc.PrintPage += (s, e) =>
        {
            var bounds = e.MarginBounds;
            e.Graphics.DrawImage(image, bounds);
        };

        printDoc.Print();
    }

    private static void ConfigureAndValidatePrintDocument(PrintDocument printDoc, string printerName, string trayName)
    {
        printDoc.PrinterSettings.PrinterName = printerName;

        if (!printDoc.PrinterSettings.IsValid)
            throw new InvalidOperationException($"Printer '{printerName}' is not valid");

        var paperSources = printDoc.PrinterSettings.PaperSources;
        if (paperSources?.Count > 0)
        {
            var trayKind = TrayKindMapper.Parse(trayName);
            var tray = paperSources.Cast<PaperSource>().FirstOrDefault(ps => ps.Kind == trayKind);
            if (tray == null)
            {
                var available = string.Join(", ", paperSources.Cast<PaperSource>().Select(p => p.SourceName));
                throw new InvalidOperationException($"Tray '{trayName}' not found. Available trays: {available}");
            }

            printDoc.DefaultPageSettings.PaperSource = tray;
        }
    }
}





