using System.Text.RegularExpressions;

namespace POS.Tests.Infrastructure;

/// <summary>
/// Validates that CI/CD and code-review configuration files are present and contain
/// the expected structural settings introduced in this PR.
/// </summary>
public sealed class CIConfigurationTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Walks up from the test binary's current directory until it finds the
    /// repository root (identified by the presence of POS.slnx).
    /// </summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (dir.GetFiles("POS.slnx").Length > 0)
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "Could not locate repository root (no POS.slnx found in any ancestor directory).");
    }

    private static string ReadConfigFile(string relativePath)
    {
        var root = FindRepoRoot();
        var fullPath = Path.Combine(root, relativePath);
        Assert.True(File.Exists(fullPath), $"Expected configuration file not found: {fullPath}");
        return File.ReadAllText(fullPath);
    }

    // ===========================================================================
    // .coderabbit.yaml
    // ===========================================================================

    [Fact]
    public void CodeRabbit_ConfigFile_Exists()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, ".coderabbit.yaml");
        Assert.True(File.Exists(path), ".coderabbit.yaml must exist at the repository root.");
    }

    [Fact]
    public void CodeRabbit_Language_IsEnUs()
    {
        var content = ReadConfigFile(".coderabbit.yaml");
        Assert.Contains("language: \"en-US\"", content);
    }

    [Fact]
    public void CodeRabbit_ReviewProfile_IsAssertive()
    {
        var content = ReadConfigFile(".coderabbit.yaml");
        Assert.Contains("profile: \"assertive\"", content);
    }

    [Fact]
    public void CodeRabbit_ReviewStatus_IsEnabled()
    {
        var content = ReadConfigFile(".coderabbit.yaml");
        Assert.Matches(new Regex(@"review_status:\s+true"), content);
    }

    [Fact]
    public void CodeRabbit_ReviewDetails_IsEnabled()
    {
        var content = ReadConfigFile(".coderabbit.yaml");
        Assert.Matches(new Regex(@"review_details:\s+true"), content);
    }

    [Fact]
    public void CodeRabbit_AutoReview_IsEnabled()
    {
        var content = ReadConfigFile(".coderabbit.yaml");
        // The 'enabled' key sits under the auto_review block.
        Assert.Matches(new Regex(@"auto_review:[\s\S]*?enabled:\s+true"), content);
    }

    [Fact]
    public void CodeRabbit_AutoReview_DraftsDisabled()
    {
        var content = ReadConfigFile(".coderabbit.yaml");
        Assert.Matches(new Regex(@"auto_review:[\s\S]*?drafts:\s+false"), content);
    }

    [Fact]
    public void CodeRabbit_PathFilters_IncludesWildcard()
    {
        var content = ReadConfigFile(".coderabbit.yaml");
        // Ensures the catch-all "**" filter is present so no files are excluded.
        Assert.Contains("- \"**\"", content);
    }

    [Fact]
    public void CodeRabbit_Chat_AutoReplyEnabled()
    {
        var content = ReadConfigFile(".coderabbit.yaml");
        Assert.Matches(new Regex(@"chat:[\s\S]*?auto_reply:\s+true"), content);
    }

    /// <summary>
    /// Regression / boundary: the file must not accidentally disable reviews
    /// by setting auto_review enabled to false.
    /// </summary>
    [Fact]
    public void CodeRabbit_AutoReview_NotDisabled()
    {
        var content = ReadConfigFile(".coderabbit.yaml");
        Assert.DoesNotMatch(new Regex(@"auto_review:[\s\S]*?enabled:\s+false"), content);
    }

    // ===========================================================================
    // .github/workflows/codeql.yml
    // ===========================================================================

    [Fact]
    public void CodeQL_WorkflowFile_Exists()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, ".github", "workflows", "codeql.yml");
        Assert.True(File.Exists(path), ".github/workflows/codeql.yml must exist.");
    }

    [Fact]
    public void CodeQL_Workflow_NameIsCodeQL()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Matches(new Regex(@"name:\s+CodeQL"), content);
    }

    [Fact]
    public void CodeQL_Triggers_PushToMain()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        // Verify main is listed under push -> branches
        Assert.Matches(new Regex(@"push:[\s\S]*?- main"), content);
    }

    [Fact]
    public void CodeQL_Triggers_PushToReviewPhase()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Matches(new Regex(@"push:[\s\S]*?- review-phase"), content);
    }

    [Fact]
    public void CodeQL_Triggers_PullRequestToMain()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Matches(new Regex(@"pull_request:[\s\S]*?- main"), content);
    }

    [Fact]
    public void CodeQL_Triggers_ScheduleIsPresent()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Contains("schedule:", content);
        Assert.Contains("cron:", content);
    }

    [Fact]
    public void CodeQL_Triggers_CronExpression()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        // Scheduled at 04:17 every Monday.
        Assert.Contains("\"17 4 * * 1\"", content);
    }

    [Fact]
    public void CodeQL_Permissions_SecurityEventsWrite()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Matches(new Regex(@"security-events:\s+write"), content);
    }

    [Fact]
    public void CodeQL_Permissions_ActionsRead()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Matches(new Regex(@"actions:\s+read"), content);
    }

    [Fact]
    public void CodeQL_Permissions_ContentsRead()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Matches(new Regex(@"contents:\s+read"), content);
    }

    [Fact]
    public void CodeQL_Runner_IsWindowsLatest()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Matches(new Regex(@"runs-on:\s+windows-latest"), content);
    }

    [Fact]
    public void CodeQL_Timeout_Is360Minutes()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Matches(new Regex(@"timeout-minutes:\s+360"), content);
    }

    [Fact]
    public void CodeQL_FailFast_IsFalse()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Matches(new Regex(@"fail-fast:\s+false"), content);
    }

    [Fact]
    public void CodeQL_LanguageMatrix_IncludesCSharp()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Matches(new Regex(@"language:\s*\r?\n\s+- csharp"), content);
    }

    [Fact]
    public void CodeQL_DotNetVersion_Is10()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Contains("dotnet-version: 10.0.x", content);
    }

    [Fact]
    public void CodeQL_BuildMode_IsManual()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Contains("build-mode: manual", content);
    }

    [Fact]
    public void CodeQL_Restore_TargetsSolutionFile()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Contains("dotnet restore POS.slnx", content);
    }

    [Fact]
    public void CodeQL_Build_UsesReleaseConfiguration()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Contains("--configuration Release", content);
    }

    [Fact]
    public void CodeQL_Build_SkipsRestoreStep()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        // --no-restore prevents a redundant restore after the dedicated step.
        Assert.Contains("--no-restore", content);
    }

    [Fact]
    public void CodeQL_Build_TargetsSolutionFile()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Contains("dotnet build POS.slnx", content);
    }

    [Fact]
    public void CodeQL_UsesCheckoutV4()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Contains("actions/checkout@v4", content);
    }

    [Fact]
    public void CodeQL_UsesSetupDotnetV4()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Contains("actions/setup-dotnet@v4", content);
    }

    [Fact]
    public void CodeQL_UsesCodeQLInitV3()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Contains("github/codeql-action/init@v3", content);
    }

    [Fact]
    public void CodeQL_UsesCodeQLAnalyzeV3()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.Contains("github/codeql-action/analyze@v3", content);
    }

    /// <summary>
    /// Regression / negative: ensure there is no accidental fail-fast: true that
    /// would prevent the full matrix from completing on a single language failure.
    /// </summary>
    [Fact]
    public void CodeQL_FailFast_IsNotTrue()
    {
        var content = ReadConfigFile(Path.Combine(".github", "workflows", "codeql.yml"));
        Assert.DoesNotMatch(new Regex(@"fail-fast:\s+true"), content);
    }
}