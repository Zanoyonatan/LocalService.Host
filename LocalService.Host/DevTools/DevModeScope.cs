using System.Runtime.InteropServices;

namespace LocalService.Host.DevTools;

internal sealed class DevModeScope : IDisposable
{
    private readonly nint _hPrinter;
    private readonly nint _pDevMode;

    public DevModeScope(string printerName, string tray)
    {
        if (!OpenPrinter(printerName, out _hPrinter, nint.Zero))
            throw new InvalidOperationException("Failed to open printer");

        int size = DocumentProperties(
            nint.Zero,
            _hPrinter,
            printerName,
            nint.Zero,
            nint.Zero,
            0);

        _pDevMode = Marshal.AllocHGlobal(size);

        _ = DocumentProperties(
            nint.Zero,
            _hPrinter,
            printerName,
            pDevModeOutput: _pDevMode,
            nint.Zero,
            DM_OUT_BUFFER);

        var devMode = Marshal.PtrToStructure<DEVMODE>(_pDevMode);

        devMode.dmFields |= DM_DEFAULTSOURCE;
        devMode.dmDefaultSource = MapTray(tray);

        Marshal.StructureToPtr(devMode, _pDevMode, true);

        DocumentProperties(
            nint.Zero,
            _hPrinter,
            printerName,
            _pDevMode,
            _pDevMode,
            DM_IN_BUFFER | DM_OUT_BUFFER);
    }

    public void Dispose()
    {
        if (_pDevMode != nint.Zero)
            Marshal.FreeHGlobal(_pDevMode);

        if (_hPrinter != nint.Zero)
            ClosePrinter(_hPrinter);
    }

    private static short MapTray(string tray) =>
        tray?.ToLowerInvariant() switch
        {
            "tray 2" => DMBIN_UPPER,
            "tray 1"=> DMBIN_LOWER,
            "tray 4"=> 4,
            "tray 14" => 14,
            "tray 15" => 15,
            "manual feeder" => DMBIN_MIDDLE,
            _ => DMBIN_AUTO
            //"upper" => DMBIN_UPPER,
            //"lower" => DMBIN_LOWER,
            //"middle" => DMBIN_MIDDLE,
            //_ => DMBIN_AUTO
        };

    // ===== Win32 =====

    private const int DM_OUT_BUFFER = 0x2;
    private const int DM_IN_BUFFER = 0x8;
    private const int DM_DEFAULTSOURCE = 0x200;

    private const short DMBIN_UPPER = 1;
    private const short DMBIN_LOWER = 2;
    private const short DMBIN_MIDDLE = 3;
    private const short DMBIN_AUTO = 7;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public short dmOrientation;
        public short dmPaperSize;
        public short dmPaperLength;
        public short dmPaperWidth;
        public short dmScale;
        public short dmCopies;
        public short dmDefaultSource;
        public short dmPrintQuality;
    }

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool OpenPrinter(
        string pPrinterName,
        out nint phPrinter,
        nint pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(nint hPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Auto)]
    private static extern int DocumentProperties(
        nint hwnd,
        nint hPrinter,
        string pDeviceName,
        nint pDevModeOutput,
        nint pDevModeInput,
        int fMode);
}
