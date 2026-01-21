# Code Analysis Tools

NetCorePal.Extensions.CodeAnalysis.Tools is a command-line tool based on the NetCorePal code analysis framework, used to generate interactive architecture visualization HTML files from .NET projects (powered by .NET 10 single-file execution).

## ⚠️ Important Notice

**Prerequisites for the tool to work**: The target project to be analyzed must reference the `NetCorePal.Extensions.CodeAnalysis` package. This package contains source generators that automatically generate metadata required for code analysis during compilation.

```xml
<PackageReference Include="NetCorePal.Extensions.CodeAnalysis" />
```

Projects without this package reference will not be able to generate analysis results.

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

### Quick Start

```bash
# Enter project directory
cd MyApp

# Auto-discover and analyze solution or projects in current directory
netcorepal-codeanalysis generate

# Specify solution (.sln/.slnx)
netcorepal-codeanalysis generate --solution MySolution.sln

# Specify project(s)
netcorepal-codeanalysis generate --project MyProject.csproj

# Customize output and title
netcorepal-codeanalysis generate --output my-architecture.html --title "My Architecture"

# Enable verbose logs
netcorepal-codeanalysis generate --verbose
```

### Command Parameters

| Option | Alias | Type | Default | Description |
|---|---|---|---|---|
| `--solution <solution>` | `-s` | File path | N/A | Solution file to analyze, `.sln`/`.slnx` |
| `--project <project>` | `-p` | File path (repeatable) | N/A | Project file(s) to analyze (`.csproj`) |
| `--output <output>` | `-o` | File path | `architecture-visualization.html` | Output HTML file path |
| `--title <title>` | `-t` | String | `架构可视化` | HTML page title |
| `--verbose` | `-v` | Switch | `false` | Enable verbose output |
| `--include-tests` | — | Switch | `false` | Include test projects (see rules below) |

#### `generate` Command

Use `generate` to analyze a solution or one or more projects and produce an interactive HTML report. Inputs can be provided by auto-discovery (top-level `.slnx/.sln/*.csproj`) or explicitly via `--solution`/`--project`. Options: see the parameter table above.

### Usage Examples

1. **Auto-discovery analysis:**

   ```bash
   # Enter project directory
   cd MyApp
   
   # Auto-discover solution or projects in current directory
   netcorepal-codeanalysis generate
   
   # Auto-discover and specify output file
   netcorepal-codeanalysis generate -o my-architecture.html
   ```

2. **Analyze specific solution:**

   ```bash
   cd MyApp
   netcorepal-codeanalysis generate \
      --solution MyApp.sln \
      --output architecture-visualization.html \
      --title "My Application Architecture"
   ```

3. **Analyze multiple projects:**

   ```bash
   cd MyApp
   netcorepal-codeanalysis generate \
      -p MyApp/MyApp.csproj \
      -p MyApp.Domain/MyApp.Domain.csproj \
      -o docs/architecture-visualization.html
   ```

   

## Auto-Discovery Mechanism

When `--solution` and `--project` are not provided, the tool auto-discovers targets in the current directory (top-level only):

- Priority: `.slnx` > `.sln` > top-level `*.csproj`
- Non-recursive scan: only load top-level solution/project files in the current directory, then recursively analyze their dependent projects
- Test projects are excluded by default (unless `--include-tests` is set)
- Visibility:
   - For `.slnx/.sln`, prints `Using solution (...): <file>` then prints `Projects to analyze (N)` listing the complete set including recursive dependencies
   - For top-level `*.csproj`, directly prints `Projects to analyze (N)` listing the complete set including recursive dependencies

> Note: The tool generates and executes a dynamic `app.cs` in an isolated temporary work directory, and runs with `--no-launch-profile` to avoid inheriting `launchSettings.json`/`global.json` from the current directory.

### Test Project Detection Rules

- Default: test projects are excluded unless `--include-tests` is specified
- A project is considered a test project if any of the following is true:
   - Any ancestor directory name is `test` or `tests` (case-insensitive)
   - The `.csproj` contains `<IsTestProject>true</IsTestProject>` (case/whitespace-insensitive)

## System Requirements

- Runtime: .NET 10 SDK (single-file execution relies on .NET 10)
- Target frameworks supported for analyzed projects: `net8.0`, `net9.0`, `net10.0`
- Projects must reference the `NetCorePal.Extensions.CodeAnalysis` package (includes source generators)

## Output Content

The tool generates interactive HTML files containing:

- **Statistics Information**: Quantity statistics and distribution of various component types
- **Architecture Overview Diagram**: Complete view of all types and their relationships in the system
- **Processing Flow Chart Collection**: Flow charts for each independent business chain (such as command processing chains)
- **Aggregate Relation Diagram Collection**: Relationship diagrams for each aggregate root
- **Interactive Navigation**: Left sidebar tree menu supporting chart type switching
- **Mermaid Live Integration**: "View in Mermaid Live" button in the upper right corner of each chart
- **📊 Version History Features** (when snapshots exist):
  - **Version Selector**: Interactive dropdown to switch between snapshot versions
  - **Historical Trends Charts** (when 2+ snapshots exist):
    - Total trends chart (total elements and relationships)
    - Element types trend chart (individual type counts over time)
    - Relationship types trend chart (relationship counts over time)
  - **Interactive Legends**: Click legend items to show/hide specific metrics
  - **Chart.js Visualization**: Professional responsive charts
  - **Filtering Consistency**: Trends use same filters as Statistics page

## Build Process Integration

### MSBuild Integration

Add to `.csproj` file:

```xml
<Target Name="GenerateArchitectureVisualization" AfterTargets="Build" Condition="'$(Configuration)' == 'Debug'">
   <Exec Command="netcorepal-codeanalysis generate --project $(MSBuildProjectFullPath) --output $(OutputPath)architecture-visualization.html" 
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
         --output docs/architecture-visualization.html \
         --title "MyApp Architecture"
```

## Troubleshooting

### Common Issues

1. **No projects discovered**: Ensure the current directory contains a `.slnx`, `.sln`, or top-level `*.csproj`, or pass `--solution/--project` explicitly
2. **No analysis results**: Ensure analyzed projects reference `NetCorePal.Extensions.CodeAnalysis`
3. **Build failures**: Ensure projects build successfully and dependencies restore correctly
4. **Permission errors**: Verify write permission for the output directory

### Verbose Output

Use the `--verbose` flag to get detailed information about the analysis process:

```bash
netcorepal-codeanalysis generate --verbose
```

This will display:

- Discovery details and chosen input (solution/projects)
- Project filtering (tests excluded unless `--include-tests`)
- Full recursive dependency list (Projects to analyze)
- Temporary work directory and single-file execution details
- Analysis statistics and output file path
- Error details when issues occur

## Related Packages

- [`NetCorePal.Extensions.CodeAnalysis`](../code-flow-analysis.md): Core analysis framework
- Source Generators: Used for automatic analysis

## Snapshot History Features (Similar to EF Core Migrations)

The tool provides version snapshot functionality to track architecture evolution history, similar to Entity Framework Core's migration mechanism.

**Snapshots are saved as C# code files**, similar to EF Core migration snapshots, making them easy to version control and code review.

### Creating Snapshots

```bash
# Create architecture snapshot for current directory (auto-discover project)
netcorepal-codeanalysis snapshot add --description "Initial version"

# Specify project file
netcorepal-codeanalysis snapshot add --project MyProject.csproj --description "Added order module"

# Specify snapshot name (EF Core style)
netcorepal-codeanalysis snapshot add --project MyProject.csproj --name "AddedPaymentFeature" --description "Added payment functionality"
```

**Snapshot File Naming**:
- Format: `Snapshot_{Version}_{Name}.cs` (follows EF Core migration naming convention)
- Version: Timestamp format `YYYYMMDDHHmmss`
- Name: Optional, derived from `--name` or `--description` (sanitized as valid identifier)
- Example: `Snapshot_20260116120000_AddedOrderModule.cs`

**Generated Snapshot Class**:
```csharp
// <auto-generated />
// Snapshot created: 2026-01-16 12:00:00
// Description: Added order module

using NetCorePal.Extensions.CodeAnalysis.Snapshots;
using NetCorePal.Extensions.CodeAnalysis.Attributes;

namespace CodeAnalysisSnapshots
{
    public partial class Snapshot_20260116120000_AddedOrderModule : CodeFlowAnalysisSnapshot
    {
        public Snapshot_20260116120000_AddedOrderModule()
        {
            Metadata = new SnapshotMetadata
            {
                Version = "20260116120000",
                Timestamp = "2026-01-16 12:00:00",
                Description = "Added order module",
                // ...
            };
            
            MetadataAttributes = new MetadataAttribute[]
            {
                new EntityMetadataAttribute("MyApp.Domain.Order", true, 
                    new string[] { "OrderItem" }, 
                    new string[] { "Create" }),
                // ... all other metadata
            };
        }
    }
}
```

### Listing Snapshots

```bash
# List all snapshots
netcorepal-codeanalysis snapshot list --snapshot-dir Snapshots

# Example output:
# Found 3 snapshot(s):
#
# Version              Timestamp              Nodes    Relationships   Description
# ----------------------------------------------------------------------------------------------------
# 20260116120000       2026-01-16 12:00:00    45       78              Added order module
# 20260115100000       2026-01-15 10:00:00    38       65              Refactored user service
# 20260114090000       2026-01-14 09:00:00    32       52              Initial version
```

### Generating HTML with History

```bash
# Generate interactive HTML with historical snapshots (auto-discovery via reflection)
netcorepal-codeanalysis generate

# Disable history feature
netcorepal-codeanalysis generate --no-history
```

**Snapshot Discovery Mechanism** (reflection-based):
- ✅ Automatically discovers all snapshot classes from project assemblies (inheriting from `CodeFlowAnalysisSnapshot`)
- ✅ Only adds new snapshots when code changes (based on hash comparison)
- ✅ Snapshots automatically sorted by version (newest first)
- ✅ No need to manually specify snapshot directory

**Generated HTML Features**:

1. **Version Selector** (shown with multiple snapshots)
   - Interactive dropdown showing snapshot descriptions and timestamps
   - Automatically refreshes all charts and statistics when switching versions
   - Professional dark theme styling

2. **Historical Trends Charts** (shown with 2+ snapshots)
   - **Total Trends Chart**: Total elements and relationships over time
   - **Element Types Trend Chart**: Aggregate, Command, DomainEvent, etc. count changes
   - **Relationship Types Trend Chart**: CommandToHandler, AggregateToDomainEvent, etc. changes
   - **Interactive Legends**: Click legend items to show/hide specific metrics
   - **Responsive Charts**: Based on Chart.js, supports zoom and detailed tooltips
   - **Chronological Timeline**: X-axis ordered from oldest to newest (left to right)
   - **Filtering Consistency**: Uses same filtering rules as Statistics page

3. **Cross-Version Synchronization**
   - All views (Statistics, Architecture, Flow charts) automatically sync across snapshots
   - Consistent user experience

### Typical Workflow

```bash
# 1. Initial architecture snapshot
netcorepal-codeanalysis snapshot add --project MyProject.csproj --description "Project initial version"

# 2. Develop new features...

# 3. Create new snapshot
netcorepal-codeanalysis snapshot add --project MyProject.csproj --description "Added payment functionality"

# 4. View snapshot history
netcorepal-codeanalysis snapshot list

# 5. Generate visualization (includes history by default)
netcorepal-codeanalysis generate --project MyProject.csproj --output architecture.html

# 6. Commit snapshots to version control (recommended)
git add Snapshots/
git commit -m "Add architecture snapshot: Added payment functionality"
```

### Version Control Integration

Snapshots are saved as C# code files and should be committed to version control:

```bash
# Add snapshot directory to version control
git add Snapshots/
git commit -m "Add architecture snapshot: [description]"
```

**Advantages**:
- ✅ Type-safe, compile-time checking
- ✅ Easy code review and diff
- ✅ Natural Git workflow integration
- ✅ Follows EF Core migration best practices
- ✅ Supports team collaboration (merge conflicts visible and resolvable)

### Snapshot Command Reference

**`snapshot add` Command**:
- `--project, -p`: Project file path (`.csproj`, required or auto-discovered)
- `--name`: Snapshot name (optional, used in filename)
- `--description, -d`: Snapshot description (required)
- `--snapshot-dir`: Snapshot directory (default: `Snapshots`)
- `--verbose, -v`: Verbose output

**Notes**:
- `snapshot add` only supports single project files (no solution files)
- If current directory contains only one `.csproj`, `--project` can be omitted
- Snapshot files are saved in project directory (relative paths resolved from project directory)

**`snapshot list` Command**:
- `--snapshot-dir`: Snapshot directory (default: `Snapshots`)
- `--verbose, -v`: Show detailed statistics

**`snapshot show` Command**:
- First argument: Snapshot version number
- `--snapshot-dir`: Snapshot directory (default: `Snapshots`)
- `--verbose, -v`: Show detailed statistics
