
namespace LocalService.Host.Infra
{
    public static class EnvironmentService
    {
        public static string Current => Environment.GetEnvironmentVariable("AVIV_ENV") ?? "D002";
        public static bool IsDevelopment => Current == "D002";
        public static bool IsTest => Current == "T019" || Current == "T019_test";
        public static bool IsProduction => Current == "A019_prod";
    }

}
