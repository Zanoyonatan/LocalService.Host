
namespace LocalService.Host.Abstractions;

    public sealed class PrintData
    {
        public required string DocumentType { get; set; }
        public required byte[] Base64bytes { get; set; }
        public required string PrinterName { get; set; }
        public required string Tray { get; set; }
    }

