namespace LocalService.Host.Abstractions
{
    public sealed class ExecuteRequest
    {
        public string ActionType { get; set; } = "";
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
    public sealed class ExecuteResult
    {
        public int HttpStatus { get; set; }
        public bool Success { get; set; }
        public string? Message { get; set; }
        public object? Data { get; set; }

        public static ExecuteResult Accepted(object? data = null, string? message = null)
            => new() { HttpStatus = 202, Success = true, Data = data, Message = message };

        public static ExecuteResult Fail(int httpStatus, string message)
            => new() { HttpStatus = httpStatus, Success = false, Message = message };
    }
}
