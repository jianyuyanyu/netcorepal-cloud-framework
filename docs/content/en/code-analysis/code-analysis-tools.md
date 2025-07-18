# Code Analysis Tools

NetCorePal.Extensions.CodeAnalysis.Tools is a command-line tool based on the NetCorePal code analysis framework, used to generate architecture visualization HTML files from .NET assemblies.

## ⚠️ Important Notice

**Prerequisites for the tool to work**: The target project/assembly to be analyzed must reference the `NetCorePal.Extensions.CodeAnalysis` package. This package contains source generators that automatically generate metadata required for code analysis during compilation.

```xml
<PackageReference Include="NetCorePal.Extensions.CodeAnalysis" Version="2.8.3" />
```

Assemblies without this package reference will not be able to generate analysis results.

## Installation

Install as a global dotnet tool:

```bash
dotnet tool install -g NetCorePal.Extensions.CodeAnalysis.Tools
```

Or install locally in a project:

```bash
dotnet tool install NetCorePal.Extensions.CodeAnalysis.Tools
```

## Usage

### Smart Discovery

The tool supports automatic discovery of solutions, projects, or assemblies in the current directory:

```bash
# Auto-discover and analyze all content in the current directory
netcorepal-codeanalysis generate

# Specify solution file
netcorepal-codeanalysis generate --solution MySolution.sln

# Specify project file  
netcorepal-codeanalysis generate --project MyProject.csproj

# Specify assembly file
netcorepal-codeanalysis generate --assembly MyApp.dll
```

### Command Line Options

#### `generate` Command

**Input Source Options (by priority):**

- `--assembly, -a`: Specify assembly files (.dll). Can be specified multiple times
- `--project, -p`: Specify project files (.csproj). Can be specified multiple times  
- `--solution, -s`: Specify solution files (.sln). Can be specified multiple times

**Build Options:**

- `--configuration, -c`: Build configuration (Debug/Release). Default: Debug

**Output Options:**

- `--output, -o`: Output HTML file path. Default: code-analysis.html
- `--title, -t`: HTML page title. Default: Architecture Visualization
- `--verbose, -v`: Enable verbose output for debugging

### Usage Examples

1. **Auto-discovery analysis:**

   ```bash
   # Enter project directory
   cd MyApp
   
   # Auto-discover solutions/projects/assemblies in current directory
   netcorepal-codeanalysis generate
   
   # Auto-discover and specify output file
   netcorepal-codeanalysis generate -o my-architecture.html
   ```

2. **Analyze specific solution:**

   ```bash
   cd MyApp
   netcorepal-codeanalysis generate \
       --solution MyApp.sln \
       --configuration Release \
       --output architecture.html \
       --title "My Application Architecture"
   ```

3. **Analyze multiple projects:**

   ```bash
   cd MyApp
   netcorepal-codeanalysis generate \
       -p MyApp/MyApp.csproj \
       -p MyApp.Domain/MyApp.Domain.csproj \
       -c Release \
       -o docs/architecture.html
   ```

4. **Direct assembly analysis:**

   ```bash
   cd MyApp
   netcorepal-codeanalysis generate \
       -a bin/Debug/net8.0/MyApp.dll \
       -a bin/Debug/net8.0/MyApp.Domain.dll \
       --verbose
   ```

## Auto-Discovery Mechanism

The tool automatically discovers project content with the following priority:

1. **Solution files**: Search for `*.sln` files
2. **Project files**: Search for `*.csproj` files  
3. **Assembly files**: Search for `*.dll` files in `bin/` directories

Discovery rules:

- Recursively search in current directory and subdirectories
- Solutions take priority over projects, projects over assemblies
- Automatically exclude test projects (containing "Test", "Tests")
- Automatically build projects and load generated assemblies

## System Requirements

- .NET 8.0 or higher
- Assemblies must contain code analysis results generated by `NetCorePal.Extensions.CodeAnalysis` source generators

## Output Content

The tool generates interactive HTML files containing:

- **Architecture Flow Charts**: Complete system architecture visualization
- **Command Chain Diagrams**: Individual command execution flows
- **Event Flow Charts**: Event-driven process visualization
- **Class Diagrams**: Type relationship charts
- **Interactive Navigation**: Tree-structured navigation between chart types
- **Mermaid Live Integration**: One-click online editing functionality

## Build Process Integration

### MSBuild Integration

Add to `.csproj` file:

```xml
<Target Name="GenerateArchitectureVisualization" AfterTargets="Build" Condition="'$(Configuration)' == 'Debug'">
  <Exec Command="netcorepal-codeanalysis generate -a $(OutputPath)$(AssemblyName).dll -o $(OutputPath)architecture.html" 
        ContinueOnError="true" />
</Target>
```

### GitHub Actions

Add to workflow:

```yaml
- name: Generate Architecture Visualization
  run: |
    dotnet tool install -g NetCorePal.Extensions.CodeAnalysis.Tools
    cd MyApp
    netcorepal-codeanalysis generate \
      --output docs/architecture.html \
      --title "MyApp Architecture"
```

## Troubleshooting

### Common Issues

1. **Assembly not found**: Ensure assembly files exist and are accessible
2. **No analysis results**: Ensure assemblies are built with `NetCorePal.Extensions.CodeAnalysis` package reference
3. **Permission errors**: Check write permissions for output directory
4. **Build failures**: Ensure projects can build normally, check dependencies

### Verbose Output

Use the `--verbose` flag to get detailed information about the analysis process:

```bash
netcorepal-codeanalysis generate --verbose
```

This will display:

- Discovered files and projects
- Build process information
- Loaded assemblies
- Analysis statistics
- File generation details
- Error details when issues occur

## Related Packages

- [`NetCorePal.Extensions.CodeAnalysis`](../code-flow-analysis.md): Core analysis framework
- Source Generators: Used for automatic analysis
