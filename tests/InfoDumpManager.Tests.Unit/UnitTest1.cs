using System.Diagnostics;

namespace InfoDumpManager.Tests.Unit;

public class SolutionStructureTests
{
    [Fact]
    public async Task SolutionBuildsSuccessfully()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var solutionPath = Path.Combine(repoRoot, "InfoDumpManager.sln");

        Assert.True(File.Exists(solutionPath), $"Solution file not found: {solutionPath}");

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build \"InfoDumpManager.sln\" -c Debug",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        Assert.True(
            process.ExitCode == 0,
            $"dotnet build failed. ExitCode: {process.ExitCode}{Environment.NewLine}{output}{Environment.NewLine}{error}");
    }
}