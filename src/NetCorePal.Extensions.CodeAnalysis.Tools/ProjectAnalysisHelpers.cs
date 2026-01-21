using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace NetCorePal.Extensions.CodeAnalysis.Tools;

internal static class ProjectAnalysisHelpers
{
    internal static bool IsTestProject(string projectFilePath, bool verbose = false)
    {
        try
        {
            // Rule 1: project in a directory named "test" or "tests" (any level)
            var dir = Path.GetDirectoryName(projectFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                var di = new DirectoryInfo(dir);
                while (di != null)
                {
                    var name = di.Name;
                    if (name.Equals("test", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("tests", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    di = di.Parent;
                }
            }

            // Rule 2: csproj contains <IsTestProject>true</IsTestProject>
            if (File.Exists(projectFilePath))
            {
                var doc = XDocument.Load(projectFilePath);
                var isTestElements = doc.Descendants("PropertyGroup")
                    .SelectMany(pg => pg.Elements("IsTestProject"))
                    .Where(elem => !string.IsNullOrEmpty(elem.Value?.Trim()))
                    .Select(elem => elem.Value.Trim());

                if (isTestElements.Any(value => value.Equals("true", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                Console.WriteLine($"    Warning: Failed to check if {Path.GetFileName(projectFilePath)} is a test project: {ex.Message}");
            }
            // best-effort heuristic; ignore parsing errors
        }

        return false;
    }

    internal static int CollectProjectDependencies(string projectPath, HashSet<string> collectedProjects, bool verbose, bool includeTests)
    {
        int missingCount = 0;

        // Avoid circular dependencies
        if (collectedProjects.Contains(projectPath))
        {
            return missingCount;
        }

        if (!includeTests && IsTestProject(projectPath, verbose))
        {
            if (verbose)
                Console.WriteLine($"  Skipping test project dependency: {Path.GetFileName(projectPath)}");
            return missingCount;
        }

        collectedProjects.Add(projectPath);

        if (verbose)
        {
            Console.WriteLine($"  Collecting: {Path.GetFileName(projectPath)}");
        }

        // Get project dependencies
        var dependencies = GetProjectDependencies(projectPath);
        foreach (var depPath in dependencies)
        {
            // Normalize path separators
            var normalizedDepPath = depPath.Replace('\\', Path.DirectorySeparatorChar);

            // Resolve relative path
            var projectDir = Path.GetDirectoryName(projectPath)!;
            var depProjectFile = Path.IsPathRooted(normalizedDepPath)
                ? normalizedDepPath
                : Path.GetFullPath(Path.Combine(projectDir, normalizedDepPath));

            if (File.Exists(depProjectFile))
            {
                missingCount += CollectProjectDependencies(depProjectFile, collectedProjects, verbose, includeTests);
            }
            else
            {
                missingCount++;
                if (verbose)
                {
                    Console.WriteLine($"    Warning: Dependency project not found: {depProjectFile}");
                }
            }
        }

        return missingCount;
    }

    internal static List<string> GetProjectPathsFromSolution(string solutionPath, string solutionDir)
    {
        var projectPaths = new List<string>();

        try
        {
            if (solutionPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                // Parse XML-based solution file
                var doc = XDocument.Load(solutionPath);
                
                var paths = doc.Descendants("Project")
                    .Select(projectElement => projectElement.Attribute("Path")?.Value)
                    .Where(pathAttr => !string.IsNullOrEmpty(pathAttr))
                    .Select(pathAttr =>
                    {
                        var absolutePath = Path.IsPathRooted(pathAttr!)
                            ? pathAttr!
                            : Path.GetFullPath(Path.Combine(solutionDir, pathAttr!.Replace('\\', Path.DirectorySeparatorChar)));
                        return absolutePath;
                    })
                    .Where(File.Exists);
                
                projectPaths.AddRange(paths);
            }
            else if (solutionPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
            {
                // Parse traditional .sln
                var lines = File.ReadAllLines(solutionPath);

                foreach (var line in lines.Where(line => line.StartsWith("Project(")))
                {
                    var lineParts = line.Split('=');
                    if (lineParts.Length < 2)
                    {
                        continue; // Skip malformed lines without '='
                    }

                    var parts = lineParts[1].Split(',');
                    if (parts.Length >= 2)
                    {
                        var projectPath = parts[1].Trim().Trim('"');
                        var absolutePath = Path.IsPathRooted(projectPath)
                            ? projectPath
                            : Path.GetFullPath(Path.Combine(solutionDir, projectPath.Replace('\\', Path.DirectorySeparatorChar)));

                        if (File.Exists(absolutePath) && absolutePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                        {
                            projectPaths.Add(absolutePath);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to parse solution file {solutionPath}: {ex.Message}");
            Console.Error.WriteLine("Falling back to directory scanning...");

            // Fallback scan
            var projectFiles = Directory.GetFiles(solutionDir, "*.csproj", SearchOption.AllDirectories);
            projectPaths.AddRange(projectFiles);
        }

        return projectPaths;
    }

    internal static List<string> GetProjectDependencies(string projectFilePath)
    {
        var dependencies = new List<string>();

        if (!File.Exists(projectFilePath))
        {
            return dependencies;
        }

        try
        {
            var doc = XDocument.Load(projectFilePath);

            dependencies.AddRange(
                doc.Descendants("ProjectReference")
                    .Select(reference => reference.Attribute("Include")?.Value?.Trim())
                    .Where(includePath => !string.IsNullOrEmpty(includePath))!);
        }
        catch (Exception)
        {
            // ignore parse errors
        }

        return dependencies;
    }

    internal static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString()
                      ?? "1.0.0";
        return version;
    }

    /// <summary>
    /// Gets the NuGet package version without the Git hash suffix
    /// </summary>
    /// <returns>Version string without Git hash (e.g., "1.0.0" from "1.0.0+abc123"), or null if version not available</returns>
    internal static string? GetVersionWithoutGithash()
    {
        var version = typeof(ProjectAnalysisHelpers).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        
        if (string.IsNullOrEmpty(version))
        {
            return null;
        }
        
        // Remove Git hash (everything after '+')
        var parts = version.Split('+');
        return parts.Length > 0 ? parts[0] : null;
    }

    /// <summary>
    /// Gets the target framework from a project file. Returns the first TargetFramework or TargetFrameworks value found.
    /// </summary>
    /// <param name="projectFilePath">Path to the .csproj file</param>
    /// <param name="verbose">Whether to print verbose output</param>
    /// <returns>Target framework (e.g., "net8.0", "net9.0") or null if not found</returns>
    internal static string? GetTargetFramework(string projectFilePath, bool verbose = false)
    {
        try
        {
            if (!File.Exists(projectFilePath))
            {
                return null;
            }

            var doc = XDocument.Load(projectFilePath);
            
            // Check for single TargetFramework
            var targetFramework = doc.Descendants("PropertyGroup")
                .SelectMany(pg => pg.Elements("TargetFramework"))
                .Where(elem => !string.IsNullOrEmpty(elem.Value?.Trim()))
                .Select(elem => elem.Value.Trim())
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(targetFramework))
            {
                if (verbose)
                {
                    Console.WriteLine($"  Found TargetFramework: {targetFramework} in {Path.GetFileName(projectFilePath)}");
                }
                return targetFramework;
            }

            // Check for multiple TargetFrameworks (take the first one)
            var targetFrameworks = doc.Descendants("PropertyGroup")
                .SelectMany(pg => pg.Elements("TargetFrameworks"))
                .Where(elem => !string.IsNullOrEmpty(elem.Value?.Trim()))
                .Select(elem => elem.Value.Trim())
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(targetFrameworks))
            {
                var frameworks = targetFrameworks.Split(';', StringSplitOptions.RemoveEmptyEntries);
                if (frameworks.Length > 0)
                {
                    var firstFramework = frameworks[0].Trim();
                    if (verbose)
                    {
                        Console.WriteLine($"  Found TargetFrameworks: {targetFrameworks} in {Path.GetFileName(projectFilePath)}, using first: {firstFramework}");
                    }
                    return firstFramework;
                }
            }

            if (verbose)
            {
                Console.WriteLine($"  No TargetFramework found in {Path.GetFileName(projectFilePath)}");
            }
        }
        catch (Exception ex)
        {
            if (verbose)
            {
                Console.WriteLine($"  Warning: Failed to get TargetFramework from {Path.GetFileName(projectFilePath)}: {ex.Message}");
            }
        }

        return null;
    }
}
