using System;
using System.IO;
using NetCorePal.Extensions.CodeAnalysis.Tools;
using Xunit;
using System.Reflection;

namespace NetCorePal.Extensions.CodeAnalysis.Tools.UnitTests;

public class ProjectAnalysisHelpersTests : IDisposable
{
    private readonly string _tempRoot;

    public ProjectAnalysisHelpersTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"codeanalysis-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            try
            {
                Directory.Delete(_tempRoot, true);
            }
            catch (IOException)
            {
                // Ignore cleanup errors
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void IsTestProject_ReturnsTrue_WhenParentDirIsTests()
    {
        var testsDir = Path.Combine(_tempRoot, "tests");
        Directory.CreateDirectory(testsDir);
        var csprojPath = Path.Combine(testsDir, "Sample.csproj");
        File.WriteAllText(csprojPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        
        var result = ProjectAnalysisHelpers.IsTestProject(csprojPath);
        Assert.True(result);
    }

    [Fact]
    public void IsTestProject_ReturnsTrue_WhenParentDirIsTests_DifferentCasing()
    {
        var testsDir = Path.Combine(_tempRoot, "TeStS");
        Directory.CreateDirectory(testsDir);
        var csprojPath = Path.Combine(testsDir, "Sample.csproj");
        File.WriteAllText(csprojPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        
        var result = ProjectAnalysisHelpers.IsTestProject(csprojPath);
        Assert.True(result);
    }

    [Fact]
    public void IsTestProject_ReturnsTrue_WhenAncestorDirIsTests()
    {
        var ancestorTestsDir = Path.Combine(_tempRoot, "tests");
        var subDir = Path.Combine(ancestorTestsDir, "submodule");
        Directory.CreateDirectory(subDir);
        var csprojPath = Path.Combine(subDir, "Sample.csproj");
        File.WriteAllText(csprojPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        
        var result = ProjectAnalysisHelpers.IsTestProject(csprojPath);
        Assert.True(result);
    }

    [Fact]
    public void IsTestProject_ReturnsFalse_WhenDirNameIsTesting()
    {
        var testingDir = Path.Combine(_tempRoot, "testing");
        Directory.CreateDirectory(testingDir);
        var csprojPath = Path.Combine(testingDir, "Sample.csproj");
        File.WriteAllText(csprojPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        
        var result = ProjectAnalysisHelpers.IsTestProject(csprojPath);
        Assert.False(result);
    }

    [Fact]
    public void IsTestProject_ReturnsTrue_WhenIsTestProjectFlagIsUppercaseAndSpaced()
    {
        var csprojPath = Path.Combine(_tempRoot, "Sample1.csproj");
        var content = """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsTestProject>  TRUE  </IsTestProject>
  </PropertyGroup>
  
</Project>
""";
        File.WriteAllText(csprojPath, content);
        
        var result = ProjectAnalysisHelpers.IsTestProject(csprojPath);
        Assert.True(result);
    }

    [Fact]
    public void IsTestProject_ReturnsTrue_WhenIsTestProjectFlagIsSet()
    {
        var csprojPath = Path.Combine(_tempRoot, "Sample2.csproj");
        var content = """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
</Project>
""";
        File.WriteAllText(csprojPath, content);
        
        var result = ProjectAnalysisHelpers.IsTestProject(csprojPath);
        Assert.True(result);
    }

    [Fact]
    public void IsTestProject_ReturnsFalse_WhenNoTestMarkers()
    {
        var csprojPath = Path.Combine(_tempRoot, "Sample3.csproj");
        var content = """
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
""";
        File.WriteAllText(csprojPath, content);
        
        var result = ProjectAnalysisHelpers.IsTestProject(csprojPath);
        Assert.False(result);
    }
}

public class ProjectAnalysisHelpersAdditionalTests
{
    [Fact]
    public void GetVersion_Returns_AssemblyInformationalVersion()
    {
        var expected = typeof(ProjectAnalysisHelpers).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(ProjectAnalysisHelpers).Assembly.GetName().Version?.ToString();

        var actual = ProjectAnalysisHelpers.GetVersion();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GetProjectDependencies_Parses_ProjectReference_Includes()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"codeanalysis-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var csprojPath = Path.Combine(tempRoot, "Sample.csproj");
        var content = """
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../Lib/Lib.csproj" />
    <ProjectReference Include="..\\Util\\Util.csproj" />
  </ItemGroup>
</Project>
""";
        File.WriteAllText(csprojPath, content);
        try
        {
            var deps = ProjectAnalysisHelpers.GetProjectDependencies(csprojPath);
            Assert.Contains("../Lib/Lib.csproj", deps);
            Assert.Contains(deps, s => s.EndsWith("Util.csproj", StringComparison.OrdinalIgnoreCase));
        }
        finally { Directory.Delete(tempRoot, true); }
    }

    [Fact]
    public void CollectProjectDependencies_Counts_Missing_Dependency()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"codeanalysis-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var mainCsproj = Path.Combine(tempRoot, "Main.csproj");
        var content = """
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="./Missing/Missing.csproj" />
  </ItemGroup>
</Project>
""";
        File.WriteAllText(mainCsproj, content);
        try
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var missing = ProjectAnalysisHelpers.CollectProjectDependencies(mainCsproj, set, verbose: true, includeTests: true);
            Assert.Equal(1, missing);
            Assert.Contains(mainCsproj, set);
        }
        finally { Directory.Delete(tempRoot, true); }
    }

    [Fact]
    public void GetProjectPathsFromSolution_Sln_Parses_Project_Path()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"codeanalysis-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        var projDir = Path.Combine(tempRoot, "src", "App");
        Directory.CreateDirectory(projDir);
        var projPath = Path.Combine(projDir, "App.csproj");
        File.WriteAllText(projPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        var slnPath = Path.Combine(tempRoot, "Sample.sln");
        var slnContent = $"Project(\"{{FAKE-GUID}}\") = \"App\", \"src\\App\\App.csproj\", \"{{GUID}}\"";
        File.WriteAllText(slnPath, slnContent);
        try
        {
            var list = ProjectAnalysisHelpers.GetProjectPathsFromSolution(slnPath, tempRoot);
            Assert.Single(list);
            Assert.Equal(projPath, list[0]);
        }
        finally { Directory.Delete(tempRoot, true); }
    }

    [Fact]
    public void GetVersionWithoutGithash_Returns_VersionWithoutGitHash()
    {
        // Act
        var version = ProjectAnalysisHelpers.GetVersionWithoutGithash();

        // Assert
        Assert.NotNull(version);
        // Version should not contain '+' character (Git hash separator)
        Assert.DoesNotContain("+", version);
        // Version should have a valid format (at least one dot for major.minor)
        Assert.Contains(".", version);
    }

    [Fact]
    public void GetVersionWithoutGithash_Strips_GitHash_From_InformationalVersion()
    {
        // Arrange - Get the actual InformationalVersion which may contain a Git hash
        var fullVersion = typeof(ProjectAnalysisHelpers).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        // Act
        var versionWithoutHash = ProjectAnalysisHelpers.GetVersionWithoutGithash();

        // Assert
        Assert.NotNull(versionWithoutHash);
        
        if (fullVersion != null && fullVersion.Contains('+'))
        {
            // If the full version contains a Git hash, verify it was stripped
            var expectedVersion = fullVersion.Split('+')[0];
            Assert.Equal(expectedVersion, versionWithoutHash);
        }
        else
        {
            // If no Git hash, version should be unchanged
            Assert.Equal(fullVersion, versionWithoutHash);
        }
    }

    [Fact]
    public void GetVersionWithoutGithash_Returns_Consistent_Value()
    {
        // Act - Call the method twice
        var version1 = ProjectAnalysisHelpers.GetVersionWithoutGithash();
        var version2 = ProjectAnalysisHelpers.GetVersionWithoutGithash();

        // Assert - Should return the same value on multiple calls
        Assert.Equal(version1, version2);
    }
}
