namespace LocalService.Host.Core;

public interface IPrinterService
{
    Task<PrintSubmitResult> PrintPdfBase64Async(string documentType, string base64, string printerName, string tray);
}

public sealed class PrintSubmitResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }

    public static PrintSubmitResult Ok() => new() { Success = true };
    public static PrintSubmitResult Fail(string error) => new() { Success = false, Error = error };
}
