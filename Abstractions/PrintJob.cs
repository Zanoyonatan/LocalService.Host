

namespace LocalService.Host.Abstractions;


    public sealed class PrintJob
    {
        public string DocumentType { get; init; } = null!;
        public string Base64 { get; init; } = null!;

        // Completion source for the dispatcher to await print result
        public TaskCompletionSource<PrintSubmitResult> Completion { get; init; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
