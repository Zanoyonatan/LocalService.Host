
using System.Drawing.Printing;

namespace LocalService.Host.Infra
{
    public static class TrayKindMapper
    {
        public static PaperSourceKind Parse(string tray)
        {
            if (string.IsNullOrWhiteSpace(tray))
                throw new ArgumentException("Tray is not Configure (empty)");

            switch (tray.Trim().ToUpperInvariant())
            {
                case "UPPER":
                case "TRAY1":
                case "TRAY 1":
                    return PaperSourceKind.Upper;

                case "LOWER":
                case "TRAY2":
                case "TRAY 2":
                    return PaperSourceKind.Lower;

                case "MP TRAY":
                case "MANUAL FEEDER":
                case "MANUAL":
                case "MP":
                    return PaperSourceKind.Manual;


                //option for 3 traies (need to run my script  -ListPrinterTrays.bat) after connet printer with 3 traies to see whice more values need to set here
                case "MIDDLE":
                    return PaperSourceKind.Middle;

                case "AUTO":
                case "AUTO SELECT":
                case "AUTOSELECT":
                case "AUTOMATICFEED":
                case "AUTOMATIC FEED":
                default:
                    return PaperSourceKind.AutomaticFeed;
            }
        }
    }
}
