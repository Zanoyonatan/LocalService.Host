using System.Runtime.InteropServices;

namespace LocalService.Host.Infra.Printing;

internal sealed class DevModeScope : IDisposable
{
    private readonly IntPtr _hPrinter;
    private readonly IntPtr _pDevMode;

    public DevModeScope(string printerName, string tray)
    {
        if (!OpenPrinter(printerName, out _hPrinter, IntPtr.Zero))
            throw new InvalidOperationException("Failed to open printer");

        int size = DocumentProperties(
            IntPtr.Zero,
            _hPrinter,
            printerName,
            IntPtr.Zero,
            IntPtr.Zero,
            0);

        _pDevMode = Marshal.AllocHGlobal(size);

        _ = DocumentProperties(
            IntPtr.Zero,
            _hPrinter,
            printerName,
            pDevModeOutput: _pDevMode,
            IntPtr.Zero,
            DM_OUT_BUFFER);

        var devMode = Marshal.PtrToStructure<DEVMODE>(_pDevMode);

        devMode.dmFields |= DM_DEFAULTSOURCE;
        devMode.dmDefaultSource = MapTray(tray);

        Marshal.StructureToPtr(devMode, _pDevMode, true);

        DocumentProperties(
            IntPtr.Zero,
            _hPrinter,
            printerName,
            _pDevMode,
            _pDevMode,
            DM_IN_BUFFER | DM_OUT_BUFFER);
    }

    public void Dispose()
    {
        if (_pDevMode != IntPtr.Zero)
            Marshal.FreeHGlobal(_pDevMode);

        if (_hPrinter != IntPtr.Zero)
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
        out IntPtr phPrinter,
        IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", CharSet = CharSet.Auto)]
    private static extern int DocumentProperties(
        IntPtr hwnd,
        IntPtr hPrinter,
        string pDeviceName,
        IntPtr pDevModeOutput,
        IntPtr pDevModeInput,
        int fMode);
}
