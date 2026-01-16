using System.CommandLine;
using System.Reflection;
using System.Xml.Linq;
using System.Text;
using NetCorePal.Extensions.CodeAnalysis;
using NetCorePal.Extensions.CodeAnalysis.Snapshots;

namespace NetCorePal.Extensions.CodeAnalysis.Tools;

public interface IExitHandler
{
    void Exit(int exitCode);
}

public class EnvironmentExitHandler : IExitHandler
{
    public void Exit(int exitCode) => Environment.Exit(exitCode);
}

public class Program
{
    private const int AnalysisTimeoutMinutes = 5;
    internal static IExitHandler ExitHandler { get; set; } = new EnvironmentExitHandler();
    
    

    public static async Task<int> Main(string[] args)
    {
        var rootCommand =
            new RootCommand(
                "NetCorePal Code Analysis Tool - Generate architecture visualization HTML files from .NET projects");

        var generateCommand = new Command("generate", "Generate HTML visualization from projects");

        var solutionOption = new Option<FileInfo>(
            name: "--solution",
            description: "Solution file to analyze (.sln)")
        {
            IsRequired = false
        };
        solutionOption.AddAlias("-s");

        var projectOption = new Option<FileInfo[]>(
            name: "--project",
            description: "Project files to analyze (.csproj)")
        {
            IsRequired = false,
            AllowMultipleArgumentsPerToken = true
        };
        projectOption.AddAlias("-p");

        var outputOption = new Option<FileInfo>(
            name: "--output",
            description: "Output HTML file path")
        {
            IsRequired = false
        };
        outputOption.AddAlias("-o");
        outputOption.SetDefaultValue(new FileInfo("architecture-visualization.html"));

        var titleOption = new Option<string>(
            name: "--title",
            description: "HTML page title")
        {
            IsRequired = false
        };
        titleOption.AddAlias("-t");
        titleOption.SetDefaultValue("架构可视化");

        var verboseOption = new Option<bool>(
            name: "--verbose",
            description: "Enable verbose output")
        {
            IsRequired = false
        };
        verboseOption.AddAlias("-v");

        var includeTestsOption = new Option<bool>(
            name: "--include-tests",
            description: "Include test projects when analyzing (default: false)")
        {
            IsRequired = false
        };
        // no short alias to avoid ambiguity

        var withHistoryOption = new Option<bool>(
            name: "--with-history",
            description: "Generate HTML with history snapshots support")
        {
            IsRequired = false
        };
        
        var snapshotDirOption = new Option<string>(
            name: "--snapshot-dir",
            description: "Snapshot directory for history mode (default: ./snapshots)")
        {
            IsRequired = false
        };
        snapshotDirOption.SetDefaultValue("snapshots");

        generateCommand.AddOption(solutionOption);
        generateCommand.AddOption(projectOption);
        generateCommand.AddOption(outputOption);
        generateCommand.AddOption(titleOption);
        generateCommand.AddOption(verboseOption);
        generateCommand.AddOption(includeTestsOption);
        generateCommand.AddOption(withHistoryOption);
        generateCommand.AddOption(snapshotDirOption);
        

        generateCommand.SetHandler(
            async (solution, projects, output, title, verbose, includeTests, withHistory, snapshotDir) =>
            {
                await GenerateVisualization(solution, projects, output, title, verbose, includeTests, withHistory, snapshotDir);
            }, solutionOption, projectOption, outputOption, titleOption, verboseOption, includeTestsOption, withHistoryOption, snapshotDirOption);

        rootCommand.AddCommand(generateCommand);
        
        // Add snapshot command
        var snapshotCommand = CreateSnapshotCommand(solutionOption, projectOption, verboseOption, includeTestsOption);
        rootCommand.AddCommand(snapshotCommand);

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task GenerateVisualization(FileInfo? solutionFile, FileInfo[]? projectFiles,
        FileInfo outputFile, string title, bool verbose, bool includeTests, bool withHistory, string snapshotDir)
    {
        try
        {
            // If with-history flag is set, generate HTML from snapshots
            if (withHistory)
            {
                if (verbose)
                {
                    Console.WriteLine($"Generating HTML with history from snapshots in: {snapshotDir}");
                }

                var storage = new SnapshotStorage(snapshotDir);
                var snapshots = storage.LoadAllSnapshots();

                if (snapshots.Count == 0)
                {
                    Console.Error.WriteLine("Error: No snapshots found. Please create a snapshot first using 'snapshot add' command.");
                    ExitHandler.Exit(1);
                    return;
                }

                var historyHtml = VisualizationHtmlBuilder.GenerateVisualizationHtmlWithHistory(
                    snapshots,
                    title);

                var outputPath = Path.GetFullPath(outputFile.FullName);
                await File.WriteAllTextAsync(outputPath, historyHtml);

                Console.WriteLine($"✅ HTML visualization with history generated successfully: {outputPath}");
                Console.WriteLine($"  {snapshots.Count} snapshot(s) included");
                return;
            }

            if (verbose)
            {
                Console.WriteLine($"NetCorePal Code Analysis Tool v{ProjectAnalysisHelpers.GetVersion()}");
                Console.WriteLine($"Output file: {outputFile.FullName}");
                Console.WriteLine($"Title: {title}");
                Console.WriteLine($"Include tests: {includeTests}");
                Console.WriteLine();
            }

            // Determine projects to analyze
            var projectsToAnalyze = new List<string>();

            if (projectFiles?.Length > 0)
            {
                // Project files specified
                if (verbose)
                    Console.WriteLine("Using specified projects:");

                foreach (var projectFile in projectFiles)
                {
                    if (!projectFile.Exists)
                    {
                        Console.Error.WriteLine($"Error: Project file not found: {projectFile.FullName}");
                        ExitHandler.Exit(1);
                    }
                    if (!includeTests && ProjectAnalysisHelpers.IsTestProject(projectFile.FullName, verbose))
                    {
                        if (verbose)
                            Console.WriteLine($"  Skipping test project: {projectFile.FullName}");
                        continue;
                    }
                    projectsToAnalyze.Add(projectFile.FullName);
                    if (verbose)
                        Console.WriteLine($"  {projectFile.FullName}");
                }
            }
            else if (solutionFile != null)
            {
                // Solution file specified
                if (!solutionFile.Exists)
                {
                    Console.Error.WriteLine($"Error: Solution file not found: {solutionFile.FullName}");
                    ExitHandler.Exit(1);
                }

                if (verbose)
                    Console.WriteLine($"Analyzing solution: {solutionFile.FullName}");

                var solutionDir = Path.GetDirectoryName(solutionFile.FullName)!;
                var projectPaths = ProjectAnalysisHelpers.GetProjectPathsFromSolution(solutionFile.FullName, solutionDir);
                
                // Skip IsTestProject check entirely when includeTests is true
                List<string> filtered;
                if (includeTests)
                {
                    filtered = projectPaths;
                }
                else
                {
                    filtered = projectPaths.Where(p => !ProjectAnalysisHelpers.IsTestProject(p)).ToList();
                    if (verbose)
                    {
                        var excluded = projectPaths.Count - filtered.Count;
                        if (excluded > 0)
                            Console.WriteLine($"Excluded {excluded} test project(s) by default.");
                    }
                }
                projectsToAnalyze.AddRange(filtered);

                if (verbose)
                {
                    Console.WriteLine($"Found {projectPaths.Count} projects:");
                    foreach (var projectPath in projectPaths)
                    {
                        Console.WriteLine($"  {Path.GetFileName(projectPath)}");
                    }
                }
            }
            else
            {
                // Auto-discover solution or projects in current directory
                if (verbose)
                    Console.WriteLine("Auto-discovering solution or projects in current directory...");

                await AutoDiscoverProjects(projectsToAnalyze, verbose, includeTests);
            }

            if (projectsToAnalyze.Count == 0)
            {
                if (projectFiles?.Length > 0 || solutionFile != null)
                {
                    // Projects were specified but all were filtered out (likely test projects)
                    Console.Error.WriteLine(
                        "Error: No non-test projects found to analyze. Use --include-tests to analyze test projects, or specify different projects.");
                }
                else
                {
                    // No projects found via auto-discovery
                    Console.Error.WriteLine(
                        "Error: No projects found to analyze. Please specify --solution or --project options.");
                }
                ExitHandler.Exit(1);
            }

            // Get all project dependencies recursively
            var allProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int totalMissing = 0;
            if (!verbose)
            {
                Console.WriteLine("Collecting project dependencies...");
            }
            foreach (var projectPath in projectsToAnalyze)
            {
                totalMissing += ProjectAnalysisHelpers.CollectProjectDependencies(projectPath, allProjects, verbose, includeTests);
            }

            if (verbose)
            {
                Console.WriteLine();
                Console.WriteLine($"Total projects to analyze (including dependencies): {allProjects.Count}");
                if (totalMissing > 0)
                {
                    Console.WriteLine($"⚠️  Warning: {totalMissing} project dependencies could not be found");
                }
            }
            else if (totalMissing > 0)
            {
                Console.WriteLine($"⚠️  Warning: {totalMissing} project dependencies could not be found (use --verbose for details)");
            }

            // Generate app.cs file in an isolated temp folder to avoid inheriting cwd/global.json
            var tempWorkDir = Path.Combine(Path.GetTempPath(), $"netcorepal-analysis-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempWorkDir);
            var tempAppCsPath = Path.Combine(tempWorkDir, "app.cs");
            var absoluteOutputPath = Path.GetFullPath(outputFile.FullName);
            var appCsContent = AppCsContentGenerator.GenerateAppCsContent(allProjects.ToList(), absoluteOutputPath, title);

            if (verbose)
            {
                Console.WriteLine();
                Console.WriteLine($"Generated app.cs at: {tempAppCsPath}");
                Console.WriteLine("Content:");
                Console.WriteLine("========================================");
                Console.WriteLine(appCsContent);
                Console.WriteLine("========================================");
                Console.WriteLine();
            }

            await File.WriteAllTextAsync(tempAppCsPath, appCsContent);

            try
            {
                Console.WriteLine("Starting analysis...");
                // Run dotnet run app.cs in an isolated temp directory to avoid project launchSettings/global.json in cwd
                var workingDir = tempWorkDir;
                var runArgs = $"run {tempAppCsPath} --no-launch-profile";
                if (verbose)
                {
                    Console.WriteLine($"Executing: dotnet {runArgs}");
                    Console.WriteLine($"WorkingDirectory: {workingDir}");
                }

                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = runArgs,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processStartInfo);
                if (process == null)
                {
                    Console.Error.WriteLine("Failed to start dotnet run process");
                    ExitHandler.Exit(1);
                    // In production, EnvironmentExitHandler.Exit(1) terminates the process.
                    // In tests, MockExitHandler does not exit, so this return prevents null-reference issues.
                    return;
                }

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                var outputTask = Task.Run(async () =>
                {
                    using var reader = process.StandardOutput;
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        outputBuilder.AppendLine(line);
                        if (verbose)
                        {
                            Console.WriteLine(line);
                        }
                    }
                });

                var errorTask = Task.Run(async () =>
                {
                    using var reader = process.StandardError;
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        errorBuilder.AppendLine(line);
                        if (verbose)
                        {
                            Console.Error.WriteLine(line);
                        }
                    }
                });

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(AnalysisTimeoutMinutes));

                try
                {
                    await Task.WhenAll(
                        process.WaitForExitAsync(timeoutCts.Token),
                        outputTask,
                        errorTask
                    );

                    if (process.ExitCode != 0)
                    {
                        var error = errorBuilder.ToString();
                        Console.Error.WriteLine($"Analysis failed with exit code {process.ExitCode}:");
                        Console.Error.WriteLine(error);
                        ExitHandler.Exit(1);
                    }

                    if (verbose)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Analysis completed successfully");
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.Error.WriteLine($"Analysis process timed out after {AnalysisTimeoutMinutes} minutes");
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Failed to kill analysis process: {ex.Message}");
                    }
                    ExitHandler.Exit(1);
                }

                // Check if output file was created
                if (File.Exists(absoluteOutputPath))
                {
                    Console.WriteLine($"✅ HTML visualization generated successfully: {absoluteOutputPath}");

                    if (verbose)
                    {
                        var fileInfo = new FileInfo(absoluteOutputPath);
                        Console.WriteLine($"File size: {fileInfo.Length:N0} bytes");
                    }
                }
                else
                {
                    Console.Error.WriteLine($"Error: Output file was not created: {absoluteOutputPath}");
                    ExitHandler.Exit(1);
                }
            }
            finally
            {
                // Clean up temporary app.cs file
                try
                {
                    if (Directory.Exists(tempWorkDir))
                    {
                        Directory.Delete(tempWorkDir, recursive: true);
                        if (verbose)
                            Console.WriteLine($"Cleaned up temporary folder: {tempWorkDir}");
                    }
                }
                catch (Exception ex)
                {
                    if (verbose)
                        Console.WriteLine($"Warning: Failed to delete temporary folder: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            ExitHandler.Exit(1);
        }
    }

    // Helpers moved to ProjectAnalysisHelpers

    

    private static async Task AutoDiscoverProjects(List<string> projectsToAnalyze, bool verbose, bool includeTests)
    {
        var currentDir = Directory.GetCurrentDirectory();

        // Prefer .slnx first, then .sln (top directory only)
        var slnxFiles = Directory.GetFiles(currentDir, "*.slnx", SearchOption.TopDirectoryOnly);
        if (slnxFiles.Length > 0)
        {
            var solutionFile = slnxFiles[0];
            Console.WriteLine($"Using solution (.slnx): {Path.GetFileName(solutionFile)}");

            var solutionDir = Path.GetDirectoryName(solutionFile)!;
            var projectPaths = ProjectAnalysisHelpers.GetProjectPathsFromSolution(solutionFile, solutionDir);
            var filtered = includeTests ? projectPaths : projectPaths.Where(p => !ProjectAnalysisHelpers.IsTestProject(p)).ToList();
            if (!includeTests && verbose)
            {
                var excluded = projectPaths.Count - filtered.Count;
                if (excluded > 0)
                    Console.WriteLine($"Excluded {excluded} test project(s) by default.");
            }

            // Collect full dependency set for display
            var allProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var proj in filtered)
            {
                ProjectAnalysisHelpers.CollectProjectDependencies(proj, allProjects, verbose, includeTests);
            }

            Console.WriteLine($"Projects to analyze ({allProjects.Count}):");
            foreach (var proj in allProjects)
            {
                Console.WriteLine($"  {Path.GetFileName(proj)}");
            }
            projectsToAnalyze.AddRange(allProjects);
            return;
        }

        var slnFiles = Directory.GetFiles(currentDir, "*.sln", SearchOption.TopDirectoryOnly);
        if (slnFiles.Length > 0)
        {
            var solutionFile = slnFiles[0];
            Console.WriteLine($"Using solution (.sln): {Path.GetFileName(solutionFile)}");

            var solutionDir = Path.GetDirectoryName(solutionFile)!;
            var projectPaths = ProjectAnalysisHelpers.GetProjectPathsFromSolution(solutionFile, solutionDir);
            var filtered = includeTests ? projectPaths : projectPaths.Where(p => !ProjectAnalysisHelpers.IsTestProject(p)).ToList();
            if (!includeTests && verbose)
            {
                var excluded = projectPaths.Count - filtered.Count;
                if (excluded > 0)
                    Console.WriteLine($"Excluded {excluded} test project(s) by default.");
            }

            // Collect full dependency set for display
            var allProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var proj in filtered)
            {
                ProjectAnalysisHelpers.CollectProjectDependencies(proj, allProjects, verbose, includeTests);
            }

            Console.WriteLine($"Projects to analyze ({allProjects.Count}):");
            foreach (var proj in allProjects)
            {
                Console.WriteLine($"  {Path.GetFileName(proj)}");
            }
            projectsToAnalyze.AddRange(allProjects);
            return;
        }

        // Look for project files
        var projectFiles = Directory.GetFiles(currentDir, "*.csproj", SearchOption.TopDirectoryOnly);
        if (projectFiles.Length > 0)
        {
            var filtered = includeTests ? projectFiles : projectFiles.Where(p => !ProjectAnalysisHelpers.IsTestProject(p)).ToArray();

            // Collect recursive dependencies to display a complete list
            var allProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var projectFile in filtered)
            {
                ProjectAnalysisHelpers.CollectProjectDependencies(projectFile, allProjects, verbose, includeTests);
            }

            Console.WriteLine($"Projects to analyze ({allProjects.Count}):");
            foreach (var proj in allProjects)
            {
                Console.WriteLine($"  {Path.GetFileName(proj)}");
            }
            projectsToAnalyze.AddRange(allProjects);

            return;
        }

        // No solution or projects found
        Console.WriteLine("  No solution or project files found in current directory.");
    }

    // Helpers moved to ProjectAnalysisHelpers

    // Helpers moved to ProjectAnalysisHelpers

    // Helpers moved to ProjectAnalysisHelpers

    private static Command CreateSnapshotCommand(
        Option<FileInfo> solutionOption,
        Option<FileInfo[]> projectOption,
        Option<bool> verboseOption,
        Option<bool> includeTestsOption)
    {
        var snapshotCommand = new Command("snapshot", "Manage analysis snapshots (similar to EF Core migrations)");

        // snapshot add command
        var addCommand = new Command("add", "Create a new snapshot of current analysis");
        var descriptionOption = new Option<string>(
            name: "--description",
            description: "Description for the snapshot")
        {
            IsRequired = false
        };
        descriptionOption.AddAlias("-d");
        descriptionOption.SetDefaultValue("Snapshot created");

        var snapshotDirOption = new Option<string>(
            name: "--snapshot-dir",
            description: "Directory to store snapshots (default: ./snapshots)")
        {
            IsRequired = false
        };
        snapshotDirOption.SetDefaultValue("snapshots");

        addCommand.AddOption(solutionOption);
        addCommand.AddOption(projectOption);
        addCommand.AddOption(descriptionOption);
        addCommand.AddOption(snapshotDirOption);
        addCommand.AddOption(verboseOption);
        addCommand.AddOption(includeTestsOption);

        addCommand.SetHandler(
            async (solution, projects, description, snapshotDir, verbose, includeTests) =>
            {
                await AddSnapshot(solution, projects, description, snapshotDir, verbose, includeTests);
            }, solutionOption, projectOption, descriptionOption, snapshotDirOption, verboseOption, includeTestsOption);

        // snapshot list command
        var listCommand = new Command("list", "List all saved snapshots");
        listCommand.AddOption(snapshotDirOption);
        listCommand.AddOption(verboseOption);

        listCommand.SetHandler((snapshotDir, verbose) =>
        {
            ListSnapshots(snapshotDir, verbose);
        }, snapshotDirOption, verboseOption);

        // snapshot show command
        var showCommand = new Command("show", "Show details of a specific snapshot");
        var versionArg = new Argument<string>("version", "Snapshot version to show");
        showCommand.AddArgument(versionArg);
        showCommand.AddOption(snapshotDirOption);
        showCommand.AddOption(verboseOption);

        showCommand.SetHandler((version, snapshotDir, verbose) =>
        {
            ShowSnapshot(version, snapshotDir, verbose);
        }, versionArg, snapshotDirOption, verboseOption);

        // snapshot diff command
        var diffCommand = new Command("diff", "Show differences between two snapshots");
        var fromVersionArg = new Argument<string>("from", "Source snapshot version");
        var toVersionArg = new Argument<string>("to", "Target snapshot version (default: latest)") { Arity = ArgumentArity.ZeroOrOne };
        diffCommand.AddArgument(fromVersionArg);
        diffCommand.AddArgument(toVersionArg);
        diffCommand.AddOption(snapshotDirOption);
        diffCommand.AddOption(verboseOption);

        diffCommand.SetHandler((fromVersion, toVersion, snapshotDir, verbose) =>
        {
            DiffSnapshots(fromVersion, toVersion, snapshotDir, verbose);
        }, fromVersionArg, toVersionArg, snapshotDirOption, verboseOption);

        snapshotCommand.AddCommand(addCommand);
        snapshotCommand.AddCommand(listCommand);
        snapshotCommand.AddCommand(showCommand);
        snapshotCommand.AddCommand(diffCommand);

        return snapshotCommand;
    }

    private static async Task AddSnapshot(FileInfo? solutionFile, FileInfo[]? projectFiles, string description,
        string snapshotDir, bool verbose, bool includeTests)
    {
        try
        {
            if (verbose)
            {
                Console.WriteLine($"Creating snapshot with description: {description}");
                Console.WriteLine($"Snapshot directory: {snapshotDir}");
            }

            // Collect projects to analyze (same logic as GenerateVisualization)
            var projectsToAnalyze = new List<string>();

            if (projectFiles?.Length > 0)
            {
                foreach (var projectFile in projectFiles)
                {
                    if (!projectFile.Exists)
                    {
                        Console.Error.WriteLine($"Error: Project file not found: {projectFile.FullName}");
                        ExitHandler.Exit(1);
                    }
                    if (!includeTests && ProjectAnalysisHelpers.IsTestProject(projectFile.FullName, verbose))
                    {
                        if (verbose)
                            Console.WriteLine($"  Skipping test project: {projectFile.FullName}");
                        continue;
                    }
                    projectsToAnalyze.Add(projectFile.FullName);
                }
            }
            else if (solutionFile != null)
            {
                if (!solutionFile.Exists)
                {
                    Console.Error.WriteLine($"Error: Solution file not found: {solutionFile.FullName}");
                    ExitHandler.Exit(1);
                }

                var solutionDir = Path.GetDirectoryName(solutionFile.FullName)!;
                var projectPaths = ProjectAnalysisHelpers.GetProjectPathsFromSolution(solutionFile.FullName, solutionDir);
                var filtered = includeTests ? projectPaths : projectPaths.Where(p => !ProjectAnalysisHelpers.IsTestProject(p)).ToList();
                projectsToAnalyze.AddRange(filtered);
            }
            else
            {
                await AutoDiscoverProjects(projectsToAnalyze, verbose, includeTests);
            }

            if (projectsToAnalyze.Count == 0)
            {
                Console.Error.WriteLine("Error: No projects found to analyze.");
                ExitHandler.Exit(1);
            }

            // Collect all project dependencies
            var allProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!verbose)
            {
                Console.WriteLine("Collecting project dependencies...");
            }
            foreach (var projectPath in projectsToAnalyze)
            {
                ProjectAnalysisHelpers.CollectProjectDependencies(projectPath, allProjects, verbose, includeTests);
            }

            // Generate analysis result using temporary app.cs
            var tempWorkDir = Path.Combine(Path.GetTempPath(), $"netcorepal-snapshot-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempWorkDir);
            var tempAppCsPath = Path.Combine(tempWorkDir, "app.cs");
            var tempOutputPath = Path.Combine(tempWorkDir, "analysis-result.json");
            
            // Generate app.cs that outputs JSON instead of HTML
            var appCsContent = GenerateSnapshotAppCsContent(allProjects.ToList(), tempOutputPath);
            await File.WriteAllTextAsync(tempAppCsPath, appCsContent);

            try
            {
                Console.WriteLine("Analyzing projects...");
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run {tempAppCsPath} --no-launch-profile",
                    WorkingDirectory = tempWorkDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processStartInfo);
                if (process == null)
                {
                    Console.Error.WriteLine("Failed to start analysis process");
                    ExitHandler.Exit(1);
                    return;
                }

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                var outputTask = Task.Run(async () =>
                {
                    using var reader = process.StandardOutput;
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        outputBuilder.AppendLine(line);
                        if (verbose) Console.WriteLine(line);
                    }
                });

                var errorTask = Task.Run(async () =>
                {
                    using var reader = process.StandardError;
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        errorBuilder.AppendLine(line);
                        if (verbose) Console.Error.WriteLine(line);
                    }
                });

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(AnalysisTimeoutMinutes));
                await Task.WhenAll(process.WaitForExitAsync(timeoutCts.Token), outputTask, errorTask);

                if (process.ExitCode != 0)
                {
                    Console.Error.WriteLine($"Analysis failed with exit code {process.ExitCode}:");
                    Console.Error.WriteLine(errorBuilder.ToString());
                    ExitHandler.Exit(1);
                }

                // Load the analysis result
                if (!File.Exists(tempOutputPath))
                {
                    Console.Error.WriteLine("Error: Analysis result file was not created");
                    ExitHandler.Exit(1);
                }

                var resultJson = await File.ReadAllTextAsync(tempOutputPath);
                var analysisResult = System.Text.Json.JsonSerializer.Deserialize<CodeFlowAnalysisResult>(resultJson,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    });

                if (analysisResult == null)
                {
                    Console.Error.WriteLine("Error: Failed to deserialize analysis result");
                    ExitHandler.Exit(1);
                    return;
                }

                // Save snapshot
                var storage = new SnapshotStorage(snapshotDir);
                var version = storage.SaveSnapshot(analysisResult, description, verbose);

                Console.WriteLine($"✅ Snapshot created successfully: {version}");
                Console.WriteLine($"  Description: {description}");
                Console.WriteLine($"  Nodes: {analysisResult.Nodes.Count}");
                Console.WriteLine($"  Relationships: {analysisResult.Relationships.Count}");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempWorkDir))
                    {
                        Directory.Delete(tempWorkDir, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    if (verbose)
                        Console.WriteLine($"Warning: Failed to delete temporary folder: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            ExitHandler.Exit(1);
        }
    }

    private static void ListSnapshots(string snapshotDir, bool verbose)
    {
        try
        {
            var storage = new SnapshotStorage(snapshotDir);
            var snapshots = storage.ListSnapshots();

            if (snapshots.Count == 0)
            {
                Console.WriteLine("No snapshots found.");
                return;
            }

            Console.WriteLine($"Found {snapshots.Count} snapshot(s):\n");
            Console.WriteLine($"{"Version",-20} {"Timestamp",-22} {"Nodes",-8} {"Relationships",-15} Description");
            Console.WriteLine(new string('-', 100));

            foreach (var snapshot in snapshots)
            {
                Console.WriteLine(
                    $"{snapshot.Version,-20} {snapshot.Timestamp:yyyy-MM-dd HH:mm:ss,-22} {snapshot.NodeCount,-8} {snapshot.RelationshipCount,-15} {snapshot.Description}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            ExitHandler.Exit(1);
        }
    }

    private static void ShowSnapshot(string version, string snapshotDir, bool verbose)
    {
        try
        {
            var storage = new SnapshotStorage(snapshotDir);
            var snapshot = storage.LoadSnapshot(version);

            if (snapshot == null)
            {
                Console.Error.WriteLine($"Error: Snapshot not found: {version}");
                ExitHandler.Exit(1);
                return;
            }

            Console.WriteLine($"Snapshot: {snapshot.Metadata.Version}");
            Console.WriteLine($"Timestamp: {snapshot.Metadata.Timestamp:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Description: {snapshot.Metadata.Description}");
            Console.WriteLine($"Hash: {snapshot.Metadata.Hash}");
            Console.WriteLine($"\nStatistics:");
            Console.WriteLine($"  Total Nodes: {snapshot.Metadata.NodeCount}");
            Console.WriteLine($"  Total Relationships: {snapshot.Metadata.RelationshipCount}");

            if (verbose)
            {
                var nodeStats = snapshot.AnalysisResult.Nodes
                    .GroupBy(n => n.Type)
                    .OrderBy(g => g.Key.ToString());

                Console.WriteLine($"\nNode Types:");
                foreach (var group in nodeStats)
                {
                    Console.WriteLine($"  {group.Key}: {group.Count()}");
                }

                var relStats = snapshot.AnalysisResult.Relationships
                    .GroupBy(r => r.Type)
                    .OrderBy(g => g.Key.ToString());

                Console.WriteLine($"\nRelationship Types:");
                foreach (var group in relStats)
                {
                    Console.WriteLine($"  {group.Key}: {group.Count()}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            ExitHandler.Exit(1);
        }
    }

    private static void DiffSnapshots(string fromVersion, string? toVersion, string snapshotDir, bool verbose)
    {
        try
        {
            var storage = new SnapshotStorage(snapshotDir);
            var fromSnapshot = storage.LoadSnapshot(fromVersion);

            if (fromSnapshot == null)
            {
                Console.Error.WriteLine($"Error: Source snapshot not found: {fromVersion}");
                ExitHandler.Exit(1);
                return;
            }

            CodeFlowAnalysisSnapshot? toSnapshot;
            if (string.IsNullOrEmpty(toVersion))
            {
                toSnapshot = storage.GetLatestSnapshot();
                if (toSnapshot == null)
                {
                    Console.Error.WriteLine("Error: No snapshots found");
                    ExitHandler.Exit(1);
                    return;
                }
            }
            else
            {
                toSnapshot = storage.LoadSnapshot(toVersion);
                if (toSnapshot == null)
                {
                    Console.Error.WriteLine($"Error: Target snapshot not found: {toVersion}");
                    ExitHandler.Exit(1);
                    return;
                }
            }

            var comparer = new SnapshotComparer();
            var comparison = comparer.Compare(fromSnapshot, toSnapshot);

            Console.WriteLine($"Comparing snapshots:");
            Console.WriteLine($"  From: {comparison.FromSnapshot?.Version} ({comparison.FromSnapshot?.Timestamp:yyyy-MM-dd HH:mm:ss})");
            Console.WriteLine($"  To:   {comparison.ToSnapshot?.Version} ({comparison.ToSnapshot?.Timestamp:yyyy-MM-dd HH:mm:ss})");
            Console.WriteLine();

            Console.WriteLine("Summary:");
            Console.WriteLine($"  Nodes:         +{comparison.AddedNodes} -{comparison.RemovedNodes} ={comparison.UnchangedNodes}");
            Console.WriteLine($"  Relationships: +{comparison.AddedRelationships} -{comparison.RemovedRelationships} ={comparison.UnchangedRelationships}");

            if (verbose || comparison.AddedNodes > 0 || comparison.RemovedNodes > 0)
            {
                Console.WriteLine();
                
                if (comparison.AddedNodes > 0)
                {
                    Console.WriteLine($"Added Nodes ({comparison.AddedNodes}):");
                    foreach (var nodeDiff in comparison.NodeDiffs.Where(d => d.DiffType == DiffType.Added))
                    {
                        Console.WriteLine($"  + [{nodeDiff.Node.Type}] {nodeDiff.Node.Name}");
                    }
                }

                if (comparison.RemovedNodes > 0)
                {
                    Console.WriteLine($"\nRemoved Nodes ({comparison.RemovedNodes}):");
                    foreach (var nodeDiff in comparison.NodeDiffs.Where(d => d.DiffType == DiffType.Removed))
                    {
                        Console.WriteLine($"  - [{nodeDiff.Node.Type}] {nodeDiff.Node.Name}");
                    }
                }

                if (comparison.AddedRelationships > 0)
                {
                    Console.WriteLine($"\nAdded Relationships ({comparison.AddedRelationships}):");
                    foreach (var relDiff in comparison.RelationshipDiffs.Where(d => d.DiffType == DiffType.Added).Take(20))
                    {
                        Console.WriteLine($"  + {relDiff.Relationship.FromNode.Name} --[{relDiff.Relationship.Type}]--> {relDiff.Relationship.ToNode.Name}");
                    }
                    if (comparison.AddedRelationships > 20)
                    {
                        Console.WriteLine($"  ... and {comparison.AddedRelationships - 20} more");
                    }
                }

                if (comparison.RemovedRelationships > 0)
                {
                    Console.WriteLine($"\nRemoved Relationships ({comparison.RemovedRelationships}):");
                    foreach (var relDiff in comparison.RelationshipDiffs.Where(d => d.DiffType == DiffType.Removed).Take(20))
                    {
                        Console.WriteLine($"  - {relDiff.Relationship.FromNode.Name} --[{relDiff.Relationship.Type}]--> {relDiff.Relationship.ToNode.Name}");
                    }
                    if (comparison.RemovedRelationships > 20)
                    {
                        Console.WriteLine($"  ... and {comparison.RemovedRelationships - 20} more");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            if (verbose)
            {
                Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            ExitHandler.Exit(1);
        }
    }

    private static string GenerateSnapshotAppCsContent(List<string> projectPaths, string outputPath)
    {
        var sb = new StringBuilder();

        // Add #:project directives
        foreach (var projectPath in projectPaths)
        {
            sb.AppendLine($"#:project {projectPath}");
        }

        sb.AppendLine();
        sb.AppendLine("using NetCorePal.Extensions.CodeAnalysis;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Reflection;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine("var baseDir = AppDomain.CurrentDomain.BaseDirectory;");

        var assemblyNames = projectPaths
            .Select(p => Path.GetFileNameWithoutExtension(p) + ".dll")
            .Distinct()
            .ToList();

        sb.AppendLine("var assemblyNames = new[]");
        sb.AppendLine("{");
        foreach (var assemblyName in assemblyNames)
        {
            sb.AppendLine($"    \"{assemblyName}\",");
        }
        sb.AppendLine("};");
        sb.AppendLine();

        sb.AppendLine("var assemblies = assemblyNames");
        sb.AppendLine("    .Select(name => Path.Combine(baseDir, name))");
        sb.AppendLine("    .Where(File.Exists)");
        sb.AppendLine("    .Select(Assembly.LoadFrom)");
        sb.AppendLine("    .Distinct()");
        sb.AppendLine("    .ToArray();");
        sb.AppendLine();

        sb.AppendLine("var result = CodeFlowAnalysisHelper.GetResultFromAssemblies(assemblies);");
        sb.AppendLine();

        var escapedOutputPath = outputPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
        sb.AppendLine("var options = new JsonSerializerOptions");
        sb.AppendLine("{");
        sb.AppendLine("    WriteIndented = true,");
        sb.AppendLine("    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,");
        sb.AppendLine("    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }");
        sb.AppendLine("};");
        sb.AppendLine($"var json = JsonSerializer.Serialize(result, options);");
        sb.AppendLine($"File.WriteAllText(@\"{escapedOutputPath}\", json);");

        return sb.ToString();
    }
}