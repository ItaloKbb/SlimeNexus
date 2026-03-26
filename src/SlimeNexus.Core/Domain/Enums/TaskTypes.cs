namespace SlimeNexus.Core.Domain.Enums;

/// <summary>
/// Well-known task types supported by SlimeNexus.
/// Use these constants instead of magic strings throughout the codebase.
/// </summary>
public static class TaskTypes
{
    /// <summary>AI-assisted code review of a file or folder.</summary>
    public const string CodeReview = "code_review";

    /// <summary>Clean up unnecessary files (bin, obj, node_modules, etc.).</summary>
    public const string FileCleanup = "file_cleanup";

    /// <summary>Validate a Git commit was made with proper message format.</summary>
    public const string GitCommit = "git_commit";

    /// <summary>Run unit tests and validate they pass.</summary>
    public const string TestRun = "test_run";

    /// <summary>Build the project and validate no errors.</summary>
    public const string BuildProject = "build_project";

    /// <summary>Generate documentation for code.</summary>
    public const string GenerateDocs = "generate_docs";

    /// <summary>Refactor code based on AI suggestions.</summary>
    public const string Refactor = "refactor";

    /// <summary>Security scan of dependencies.</summary>
    public const string SecurityScan = "security_scan";

    /// <summary>Custom task defined by user prompt.</summary>
    public const string Custom = "custom";

    /// <summary>All known task types.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        CodeReview,
        FileCleanup,
        GitCommit,
        TestRun,
        BuildProject,
        GenerateDocs,
        Refactor,
        SecurityScan,
        Custom
    ];

    /// <summary>Checks if a task type is valid.</summary>
    public static bool IsValid(string taskType) =>
        !string.IsNullOrWhiteSpace(taskType) && All.Contains(taskType);
}
