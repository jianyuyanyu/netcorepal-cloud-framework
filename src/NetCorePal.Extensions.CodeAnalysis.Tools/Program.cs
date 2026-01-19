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
    
    /// <summary>
    /// Searches for NuGet.config file starting from current directory and walking up the directory tree.
    /// Also searches in the project root directory if specified.
    /// Returns the path to the found NuGet.config, or null if not found.
    /// </summary>
    /// <param name="projectRootDir">Optional project root directory (solution or project directory)</param>
    /// <param name="verbose">Whether to print verbose output</param>
    /// <returns>Path to NuGet.config if found, otherwise null</returns>
    private static string? FindNuGetConfig(string? projectRootDir, bool verbose)
    {
        var searchedDirs = new List<string>();
        
        // Strategy 1: Search from current directory up the tree
        var currentDir = Directory.GetCurrentDirectory();
        if (verbose)
        {
            Console.WriteLine($"Searching for NuGet.config...");
            Console.WriteLine($"  Current directory: {currentDir}");
        }
        
        var dir = currentDir;
        while (!string.IsNullOrEmpty(dir))
        {
            var nugetConfigPath = Path.Combine(dir, "NuGet.config");
            searchedDirs.Add(dir);
            
            if (verbose)
            {
                Console.WriteLine($"  Checking: {nugetConfigPath}");
            }
            
            if (File.Exists(nugetConfigPath))
            {
                if (verbose)
                {
                    Console.WriteLine($"  ✓ Found NuGet.config at: {nugetConfigPath}");
                }
                return nugetConfigPath;
            }
            
            var parentDir = Path.GetDirectoryName(dir);
            if (parentDir == dir || string.IsNullOrEmpty(parentDir))
            {
                break; // Reached root
            }
            dir = parentDir;
        }
        
        // Strategy 2: Check project root directory if specified and not already searched
        if (!string.IsNullOrEmpty(projectRootDir) && !searchedDirs.Contains(projectRootDir))
        {
            var nugetConfigPath = Path.Combine(projectRootDir, "NuGet.config");
            if (verbose)
            {
                Console.WriteLine($"  Checking project root: {nugetConfigPath}");
            }
            
            if (File.Exists(nugetConfigPath))
            {
                if (verbose)
                {
                    Console.WriteLine($"  ✓ Found NuGet.config at: {nugetConfigPath}");
                }
                return nugetConfigPath;
            }
        }
        
        if (verbose)
        {
            Console.WriteLine($"  ✗ NuGet.config not found in any searched directories");
        }
        
        return null;
    }
    
    /// <summary>
    /// Copies NuGet.config to the destination directory if found.
    /// Searches from current directory up the tree, and also checks project root.
    /// </summary>
    /// <param name="projectRootDir">Optional project root directory (solution or project directory)</param>
    /// <param name="destDir">Temporary directory where app.cs will be executed</param>
    /// <param name="verbose">Whether to print verbose output</param>
    private static void CopyNuGetConfigIfExists(string? projectRootDir, string destDir, bool verbose)
    {
        var nugetConfigPath = FindNuGetConfig(projectRootDir, verbose);
        
        if (nugetConfigPath != null)
        {
            var destPath = Path.Combine(destDir, "NuGet.config");
            File.Copy(nugetConfigPath, destPath, overwrite: true);
            if (verbose)
            {
                Console.WriteLine($"  → Copied NuGet.config to: {destPath}");
            }
        }
    }

    /// <summary>
    /// Determines the root directory for the project(s) being analyzed.
    /// Returns the solution directory if a solution is specified, otherwise the directory of the first project.
    /// </summary>
    private static string? DetermineProjectRootDirectory(FileInfo? solutionFile, IEnumerable<string> projectPaths)
    {
        if (solutionFile != null && solutionFile.Exists)
        {
            return Path.GetDirectoryName(solutionFile.FullName);
        }
        else
        {
            var firstProject = projectPaths.FirstOrDefault();
            if (firstProject != null)
            {
                return Path.GetDirectoryName(firstProject);
            }
        }
        return null;
    }
    
    

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

        var noHistoryOption = new Option<bool>(
            name: "--no-history",
            description: "Disable history snapshots support (history is enabled by default)")
        {
            IsRequired = false
        };
        
        var snapshotDirOption = new Option<string>(
            name: "--snapshot-dir",
            description: "Snapshot directory for history mode (default: ./Snapshots)")
        {
            IsRequired = false
        };
        snapshotDirOption.SetDefaultValue("Snapshots");

        generateCommand.AddOption(solutionOption);
        generateCommand.AddOption(projectOption);
        generateCommand.AddOption(outputOption);
        generateCommand.AddOption(titleOption);
        generateCommand.AddOption(verboseOption);
        generateCommand.AddOption(includeTestsOption);
        generateCommand.AddOption(noHistoryOption);
        generateCommand.AddOption(snapshotDirOption);
        

        generateCommand.SetHandler(
            async (solution, projects, output, title, verbose, includeTests, noHistory, snapshotDir) =>
            {
                await GenerateVisualization(solution, projects, output, title, verbose, includeTests, !noHistory, snapshotDir);
            }, solutionOption, projectOption, outputOption, titleOption, verboseOption, includeTestsOption, noHistoryOption, snapshotDirOption);

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
            if (verbose)
            {
                Console.WriteLine($"NetCorePal Code Analysis Tool v{ProjectAnalysisHelpers.GetVersion()}");
                Console.WriteLine($"Output file: {outputFile.FullName}");
                Console.WriteLine($"Title: {title}");
                Console.WriteLine($"Include tests: {includeTests}");
                Console.WriteLine($"History mode: {withHistory}");
                Console.WriteLine();
            }

            // Load snapshots if history is enabled
            System.Collections.Generic.List<Snapshots.CodeFlowAnalysisSnapshot>? snapshots = null;
            if (withHistory)
            {
                snapshots = LoadAllSnapshotFiles(snapshotDir);
                if (snapshots.Count > 0 && verbose)
                {
                    Console.WriteLine($"Loaded {snapshots.Count} snapshot(s) from: {snapshotDir}");
                }
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
            
            // Copy NuGet.config if it exists (searches current dir and parents, plus project root)
            var projectRootDir = DetermineProjectRootDirectory(solutionFile, allProjects);
            CopyNuGetConfigIfExists(projectRootDir, tempWorkDir, verbose);
            
            var tempAppCsPath = Path.Combine(tempWorkDir, "app.cs");
            var absoluteOutputPath = Path.GetFullPath(outputFile.FullName);
            var appCsContent = AppCsContentGenerator.GenerateAppCsContent(
                allProjects.ToList(), 
                absoluteOutputPath, 
                title,
                withHistory,
                snapshotDir);

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
                
                // Determine target framework from first project
                string? targetFramework = null;
                if (allProjects.Count > 0)
                {
                    var firstProject = allProjects.First();
                    targetFramework = ProjectAnalysisHelpers.GetTargetFramework(firstProject, verbose);
                }
                
                // Run dotnet run app.cs in an isolated temp directory to avoid project launchSettings/global.json in cwd
                var workingDir = tempWorkDir;
                var runArgs = $"run {tempAppCsPath} --no-launch-profile";
                
                // Add framework parameter if found
                if (!string.IsNullOrEmpty(targetFramework))
                {
                    runArgs += $" -f {targetFramework}";
                    if (verbose)
                    {
                        Console.WriteLine($"Using target framework: {targetFramework}");
                    }
                }
                
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

        var nameOption = new Option<string>(
            name: "--name",
            description: "Name for the snapshot (used in filename, e.g., InitialCreate)")
        {
            IsRequired = false
        };
        nameOption.AddAlias("-n");

        var snapshotDirOption = new Option<string>(
            name: "--snapshot-dir",
            description: "Directory to store snapshots (default: ./Snapshots)")
        {
            IsRequired = false
        };
        snapshotDirOption.SetDefaultValue("Snapshots");

        // NOTE: snapshot add command does NOT support solution files
        // Only support --project option and auto-discovery of single .csproj
        addCommand.AddOption(projectOption);
        addCommand.AddOption(descriptionOption);
        addCommand.AddOption(nameOption);
        addCommand.AddOption(snapshotDirOption);
        addCommand.AddOption(verboseOption);
        addCommand.AddOption(includeTestsOption);

        addCommand.SetHandler(
            async (projects, description, name, snapshotDir, verbose, includeTests) =>
            {
                await AddSnapshot(projects, description, name, snapshotDir, verbose, includeTests);
            }, projectOption, descriptionOption, nameOption, snapshotDirOption, verboseOption, includeTestsOption);

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

        snapshotCommand.AddCommand(addCommand);
        snapshotCommand.AddCommand(listCommand);
        snapshotCommand.AddCommand(showCommand);

        return snapshotCommand;
    }

    private static async Task AddSnapshot(FileInfo[]? projectFiles, string description,
        string? name, string snapshotDir, bool verbose, bool includeTests)
    {
        try
        {
            if (verbose)
            {
                Console.WriteLine($"Creating snapshot with description: {description}");
                Console.WriteLine($"Snapshot directory: {snapshotDir}");
            }

            // Collect projects to analyze
            // NOTE: snapshot add command does NOT support solution files
            var projectsToAnalyze = new List<string>();

            if (projectFiles?.Length > 0)
            {
                // Specific project files provided
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
            else
            {
                // Auto-discover: only allow if current directory has exactly ONE .csproj
                var currentDir = Directory.GetCurrentDirectory();
                var projectFiles2 = Directory.GetFiles(currentDir, "*.csproj", SearchOption.TopDirectoryOnly);
                
                if (projectFiles2.Length == 0)
                {
                    Console.Error.WriteLine("Error: No .csproj file found in current directory.");
                    Console.Error.WriteLine("Snapshot command requires either:");
                    Console.Error.WriteLine("  1. Explicit --project option with specific .csproj file(s)");
                    Console.Error.WriteLine("  2. Current directory containing exactly one .csproj file");
                    ExitHandler.Exit(1);
                }
                else if (projectFiles2.Length > 1)
                {
                    Console.Error.WriteLine($"Error: Found {projectFiles2.Length} .csproj files in current directory.");
                    Console.Error.WriteLine("Snapshot command requires exactly one .csproj file when auto-discovering.");
                    Console.Error.WriteLine("Please use --project option to specify which project to analyze.");
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("Found projects:");
                    foreach (var proj in projectFiles2)
                    {
                        Console.Error.WriteLine($"  {Path.GetFileName(proj)}");
                    }
                    ExitHandler.Exit(1);
                }
                
                // Exactly one project found
                var singleProject = projectFiles2[0];
                if (!includeTests && ProjectAnalysisHelpers.IsTestProject(singleProject, verbose))
                {
                    Console.Error.WriteLine($"Error: The single .csproj file found is a test project: {Path.GetFileName(singleProject)}");
                    Console.Error.WriteLine("Snapshot command does not support test projects by default.");
                    Console.Error.WriteLine("Use --include-tests flag if you want to create a snapshot from a test project.");
                    ExitHandler.Exit(1);
                }
                
                if (verbose)
                {
                    Console.WriteLine($"Auto-discovered project: {Path.GetFileName(singleProject)}");
                }
                projectsToAnalyze.Add(singleProject);
            }

            if (projectsToAnalyze.Count == 0)
            {
                Console.Error.WriteLine("Error: No projects found to analyze after filtering.");
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

            // Generate version number
            var version = DateTime.Now.ToString("yyyyMMddHHmmss");
            
            // Ensure snapshot directory exists
            var absoluteSnapshotDir = Path.GetFullPath(snapshotDir);
            if (!Directory.Exists(absoluteSnapshotDir))
            {
                Directory.CreateDirectory(absoluteSnapshotDir);
                if (verbose)
                    Console.WriteLine($"Created snapshot directory: {absoluteSnapshotDir}");
            }

            // Generate app.cs that creates snapshot .cs file directly
            var tempWorkDir = Path.Combine(Path.GetTempPath(), $"netcorepal-snapshot-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempWorkDir);
            
            // Copy NuGet.config if it exists (searches current dir and parents, plus project root)
            var projectRootDir = DetermineProjectRootDirectory(null, allProjects); // No solution file in snapshot command
            CopyNuGetConfigIfExists(projectRootDir, tempWorkDir, verbose);
            
            var tempAppCsPath = Path.Combine(tempWorkDir, "app.cs");
            
            // Generate app.cs that creates snapshot file
            var appCsContent = SnapshotAppCsGenerator.GenerateSnapshotAppCsContent(
                allProjects.ToList(), 
                absoluteSnapshotDir, 
                description,
                version,
                name);
            await File.WriteAllTextAsync(tempAppCsPath, appCsContent);

            if (verbose)
            {
                Console.WriteLine();
                Console.WriteLine($"Generated app.cs file:");
                Console.WriteLine($"  Directory: {tempWorkDir}");
                Console.WriteLine($"  Path: {tempAppCsPath}");
                Console.WriteLine();
                Console.WriteLine("--- app.cs Content ---");
                Console.WriteLine(appCsContent);
                Console.WriteLine("--- End of app.cs ---");
                Console.WriteLine();
            }

            try
            {
                Console.WriteLine("Creating snapshot...");
                
                // Determine target framework from first project
                string? targetFramework = null;
                if (allProjects.Count > 0)
                {
                    var firstProject = allProjects.First();
                    targetFramework = ProjectAnalysisHelpers.GetTargetFramework(firstProject, verbose);
                }
                
                var runArgs = $"run {tempAppCsPath} --no-launch-profile";
                
                // Add framework parameter if found
                if (!string.IsNullOrEmpty(targetFramework))
                {
                    runArgs += $" -f {targetFramework}";
                    if (verbose)
                    {
                        Console.WriteLine($"Using target framework: {targetFramework}");
                    }
                }
                
                if (verbose)
                {
                    Console.WriteLine($"Executing: dotnet {runArgs}");
                    Console.WriteLine($"WorkingDirectory: {tempWorkDir}");
                }
                
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = runArgs,
                    WorkingDirectory = tempWorkDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processStartInfo);
                if (process == null)
                {
                    Console.Error.WriteLine("Failed to start snapshot creation process");
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
                        Console.WriteLine(line); // Always show output for snapshot creation
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
                    Console.Error.WriteLine($"Snapshot creation failed with exit code {process.ExitCode}:");
                    Console.Error.WriteLine(errorBuilder.ToString());
                    ExitHandler.Exit(1);
                }

                // Snapshot file has been created by the app.cs
                Console.WriteLine($"✅ Snapshot created successfully: {version}");
                Console.WriteLine($"  Snapshot file: Snapshot_{version}.cs");
                Console.WriteLine($"  Description: {description}");
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
            var snapshots = ListSnapshotFiles(snapshotDir);

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
            var snapshot = LoadSnapshotFile(version, snapshotDir);

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

    // Helper methods for snapshot operations (extracted from SnapshotStorage)
    
    private static string SaveSnapshotToFile(CodeFlowAnalysisResult analysisResult, string description, string snapshotDir, bool verbose)
    {
        // 创建快照目录
        if (!Directory.Exists(snapshotDir))
        {
            Directory.CreateDirectory(snapshotDir);
            if (verbose)
                Console.WriteLine($"Created snapshot directory: {snapshotDir}");
        }

        // 生成版本号（基于时间戳）
        var version = DateTime.Now.ToString("yyyyMMddHHmmss");
        
        // 计算哈希值
        var hash = ComputeSnapshotHash(analysisResult);

        // 创建快照元数据
        var metadata = new SnapshotMetadata
        {
            Version = version,
            Timestamp = DateTime.Now,
            Description = description,
            Hash = hash,
            NodeCount = analysisResult.Nodes.Count,
            RelationshipCount = analysisResult.Relationships.Count
        };

        // 生成C#代码
        var csharpCode = CodeFlowAnalysisSnapshotHelper.GenerateSnapshotCode(analysisResult, metadata);
        
        // 保存到.cs文件
        var fileName = $"Snapshot_{version}.cs";
        var filePath = Path.Combine(snapshotDir, fileName);
        File.WriteAllText(filePath, csharpCode);

        if (verbose)
        {
            Console.WriteLine($"Snapshot saved: {filePath}");
            Console.WriteLine($"  Version: {version}");
            Console.WriteLine($"  Nodes: {metadata.NodeCount}");
            Console.WriteLine($"  Relationships: {metadata.RelationshipCount}");
        }

        return version;
    }

    private static List<SnapshotMetadata> ListSnapshotFiles(string snapshotDir)
    {
        if (!Directory.Exists(snapshotDir))
        {
            return new List<SnapshotMetadata>();
        }

        var files = Directory.GetFiles(snapshotDir, "Snapshot_*.cs")
            .OrderByDescending(f => f);

        var snapshots = new List<SnapshotMetadata>();
        foreach (var file in files)
        {
            try
            {
                // Extract version from filename
                var fileName = Path.GetFileNameWithoutExtension(file);
                var version = fileName.Replace("Snapshot_", "");
                
                // 从文件内容中解析元数据（避免加载整个快照）
                var metadata = ExtractMetadataFromSnapshotFile(file, version);
                if (metadata != null)
                {
                    snapshots.Add(metadata);
                }
            }
            catch
            {
                // Skip files that cannot be loaded
            }
        }

        return snapshots;
    }

    private static CodeFlowAnalysisSnapshot? LoadSnapshotFile(string version, string snapshotDir)
    {
        var fileName = $"Snapshot_{version}.cs";
        var filePath = Path.Combine(snapshotDir, fileName);

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            // 使用app.cs方式加载快照
            return LoadSnapshotViaAppCs(filePath, version);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading snapshot {version}: {ex.Message}");
            return null;
        }
    }

    private static List<CodeFlowAnalysisSnapshot> LoadAllSnapshotFiles(string snapshotDir)
    {
        var metadata = ListSnapshotFiles(snapshotDir);
        var snapshots = new List<CodeFlowAnalysisSnapshot>();
        
        foreach (var meta in metadata)
        {
            var snapshot = LoadSnapshotFile(meta.Version, snapshotDir);
            if (snapshot != null)
            {
                snapshots.Add(snapshot);
            }
        }
        
        return snapshots;
    }

    private static CodeFlowAnalysisSnapshot? LoadSnapshotViaAppCs(string snapshotFilePath, string version)
    {
        // 创建临时工作目录
        var tempWorkDir = Path.Combine(Path.GetTempPath(), $"netcorepal-snapshot-load-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempWorkDir);

        try
        {
            // 生成app.cs文件
            var tempOutputPath = Path.Combine(tempWorkDir, "snapshot-output.json");
            var appCsContent = GenerateSnapshotLoaderAppCs(snapshotFilePath, version, tempOutputPath);
            var tempAppCsPath = Path.Combine(tempWorkDir, "app.cs");
            
            File.WriteAllText(tempAppCsPath, appCsContent);

            // 执行app.cs
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
                Console.Error.WriteLine("Failed to start dotnet process for snapshot loading");
                return null;
            }

            var output = new StringBuilder();
            var error = new StringBuilder();
            
            process.OutputDataReceived += (sender, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) error.AppendLine(e.Data); };
            
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            
            process.WaitForExit(60000); // 60 second timeout

            if (process.ExitCode != 0)
            {
                Console.Error.WriteLine($"Failed to load snapshot (exit code {process.ExitCode}):");
                Console.Error.WriteLine($"Output: {output}");
                Console.Error.WriteLine($"Error: {error}");
                return null;
            }

            // 读取序列化的快照
            if (!File.Exists(tempOutputPath))
            {
                Console.Error.WriteLine($"Output file not created: {tempOutputPath}");
                Console.Error.WriteLine($"Process output: {output}");
                return null;
            }

            var json = File.ReadAllText(tempOutputPath);
            var snapshot = System.Text.Json.JsonSerializer.Deserialize<CodeFlowAnalysisSnapshot>(json,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                });

            return snapshot;
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
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private static string GenerateSnapshotLoaderAppCs(string snapshotFilePath, string version, string outputPath)
    {
        var sb = new StringBuilder();
        
        // 添加项目引用（需要引用CodeAnalysis项目来加载快照类型）
        var codeAnalysisAssemblyPath = typeof(CodeFlowAnalysisResult).Assembly.Location;
        var codeAnalysisProjectPath = Path.Combine(
            Path.GetDirectoryName(codeAnalysisAssemblyPath)!,
            "..", "..", "..", "..", "..",
            "src", "NetCorePal.Extensions.CodeAnalysis", "NetCorePal.Extensions.CodeAnalysis.csproj"
        );
        var resolvedProjectPath = Path.GetFullPath(codeAnalysisProjectPath);
        
        if (File.Exists(resolvedProjectPath))
        {
            sb.AppendLine($"#:project {resolvedProjectPath}");
            sb.AppendLine();
        }
        
        // 复制快照文件内容
        sb.AppendLine(File.ReadAllText(snapshotFilePath));
        sb.AppendLine();
        
        // 添加加载和序列化逻辑
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"var snapshot = CodeAnalysisSnapshots.Snapshot_{version}.BuildSnapshot();");
        sb.AppendLine();
        sb.AppendLine("var options = new JsonSerializerOptions");
        sb.AppendLine("{");
        sb.AppendLine("    WriteIndented = true,");
        sb.AppendLine("    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,");
        sb.AppendLine("    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }");
        sb.AppendLine("};");
        sb.AppendLine();
        var escapedPath = outputPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
        sb.AppendLine($"var json = JsonSerializer.Serialize(snapshot, options);");
        sb.AppendLine($"File.WriteAllText(@\"{escapedPath}\", json);");
        sb.AppendLine($"Console.WriteLine($\"Snapshot {version} loaded and serialized\");");
        
        return sb.ToString();
    }

    private static SnapshotMetadata? ExtractMetadataFromSnapshotFile(string filePath, string version)
    {
        try
        {
            var lines = File.ReadAllLines(filePath);
            var metadata = new SnapshotMetadata { Version = version };

            foreach (var line in lines.Take(20)) // Only check first 20 lines
            {
                if (line.Contains("Snapshot created:"))
                {
                    var dateStr = line.Split(new[] { "Snapshot created:" }, StringSplitOptions.None)[1].Trim();
                    if (DateTime.TryParse(dateStr, out var timestamp))
                    {
                        metadata.Timestamp = timestamp;
                    }
                }
                else if (line.Contains("Description:"))
                {
                    metadata.Description = line.Split(new[] { "Description:" }, StringSplitOptions.None)[1].Trim();
                }
                else if (line.Contains("NodeCount ="))
                {
                    var countStr = line.Split('=')[1].Trim().TrimEnd(',');
                    if (int.TryParse(countStr, out var count))
                    {
                        metadata.NodeCount = count;
                    }
                }
                else if (line.Contains("RelationshipCount ="))
                {
                    var countStr = line.Split('=')[1].Trim();
                    if (int.TryParse(countStr, out var count))
                    {
                        metadata.RelationshipCount = count;
                    }
                }
                else if (line.Contains("Hash ="))
                {
                    var hashStr = line.Split('=')[1].Trim().Trim('"').TrimEnd(',');
                    metadata.Hash = hashStr;
                }
            }

            return metadata;
        }
        catch
        {
            return null;
        }
    }

    private static string ComputeSnapshotHash(CodeFlowAnalysisResult analysisResult)
    {
        var sb = new StringBuilder();
        
        // 对节点排序后计算哈希
        foreach (var node in analysisResult.Nodes.OrderBy(n => n.Id))
        {
            sb.Append($"{node.Id}|{node.Name}|{node.Type}|");
        }
        
        // 对关系排序后计算哈希
        foreach (var rel in analysisResult.Relationships.OrderBy(r => r.FromNode.Id).ThenBy(r => r.ToNode.Id))
        {
            sb.Append($"{rel.FromNode.Id}->{rel.ToNode.Id}|{rel.Type}|");
        }

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}