namespace SlimeNexus.Core.Domain.Enums;

/// <summary>
/// Error codes returned in ValidationResult.ErrorCode for failed tasks.
/// </summary>
public static class ErrorCodes
{
    // Validation Errors (1xx)
    public const string InvalidTaskType = "ERR_101_INVALID_TASK_TYPE";
    public const string InvalidMetadata = "ERR_102_INVALID_METADATA";
    public const string PreconditionFailed = "ERR_103_PRECONDITION_FAILED";
    public const string PathNotFound = "ERR_104_PATH_NOT_FOUND";
    public const string PermissionDenied = "ERR_105_PERMISSION_DENIED";

    // Execution Errors (2xx)
    public const string ExecutionTimeout = "ERR_201_EXECUTION_TIMEOUT";
    public const string ExecutorNotFound = "ERR_202_EXECUTOR_NOT_FOUND";
    public const string ExecutionFailed = "ERR_203_EXECUTION_FAILED";
    public const string TaskCancelled = "ERR_204_TASK_CANCELLED";

    // AI Errors (3xx)
    public const string AiServiceUnavailable = "ERR_301_AI_SERVICE_UNAVAILABLE";
    public const string AiInferenceFailed = "ERR_302_AI_INFERENCE_FAILED";
    public const string ModelNotFound = "ERR_303_MODEL_NOT_FOUND";
    public const string InsufficientVram = "ERR_304_INSUFFICIENT_VRAM";

    // Pet State Errors (4xx)
    public const string PetCannotPerformTask = "ERR_401_PET_CANNOT_PERFORM_TASK";
    public const string PetExhausted = "ERR_402_PET_EXHAUSTED";

    // System Errors (5xx)
    public const string UnexpectedError = "ERR_500_UNEXPECTED_ERROR";
    public const string HardwareProbeError = "ERR_501_HARDWARE_PROBE_ERROR";
}
