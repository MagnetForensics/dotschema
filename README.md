# DotSchema

A .NET tool that generates C# code from JSON schemas, with support for detecting shared and variant-specific types
across multiple schema files.

## Features

- **JSON Schema to C# code generation** using NJsonSchema
- **Multi-schema analysis** to detect shared vs variant-specific types
- **Three generation modes:**
    - `All` - Generates shared types + variant-specific types for all variants
    - `Shared` - Generates only types that exist in all provided schemas
    - `Variant` - Generates only types unique to a specific variant
- **Automatic interface generation** for variant types
- **Roslyn-based code formatting** for clean, consistent output

## Requirements

- .NET 9.0 SDK or later

## Installation

### As a global tool

```bash
dotnet tool install --global DotSchema
```

### As a local tool

```bash
dotnet new tool-manifest # if you don't have one already
dotnet tool install DotSchema
```

### From source

```bash
dotnet tool restore
dotnet build
```

## Usage

```bash
# If installed as a tool
dotschema [options]

# If running from source
dotnet run -- [options]
```

### Options

| Option           | Short | Required | Description                                                     |
| ---------------- | ----- | -------- | --------------------------------------------------------------- |
| `--schemas`      | `-s`  | Yes      | One or more JSON schema files to process                        |
| `--output`       | `-o`  | Yes      | Output file path (Shared/Variant) or directory (All mode)       |
| `--namespace`    | `-n`  | Yes      | Namespace for generated types                                   |
| `--mode`         | `-m`  | No       | Generation mode: `All`, `Shared`, or `Variant` (default: `All`) |
| `--variant`      | `-v`  | No       | Variant name for single-variant generation                      |
| `--no-interface` |       | No       | Skip generating the marker interface                            |
| `--verbose`      |       | No       | Enable verbose output (debug-level logging)                     |
| `--quiet`        | `-q`  | No       | Suppress non-error output                                       |
| `--dry-run`      |       | No       | Preview what would be generated without writing files           |

### Examples

**Generate all types from multiple schemas:**

```bash
dotnet run -- -s windows.schema.json linux.schema.json -o ./Generated -n MyApp.Config
```

**Generate only shared types:**

```bash
dotnet run -- -m Shared -s windows.schema.json linux.schema.json -o SharedConfig.cs -n MyApp.Config
```

**Generate variant-specific types:**

```bash
dotnet run -- -m Variant -v Windows -s windows.schema.json linux.schema.json -o WindowsConfig.cs -n MyApp.Config
```

## Output

In `All` mode, the tool generates:

- `I{RootType}.cs` - Marker interface implemented by all variant types
- `Shared{RootType}.cs` - Types common to all schemas
- `{Variant}{RootType}.cs` - Variant-specific types for each schema

## Architecture

The codebase is organized into several key components:

```
DotSchema/
├── Program.cs                 # Entry point, CLI parsing
├── CommandLineOptions.cs      # CLI option definitions
├── Constants.cs               # Shared constants and naming utilities
├── CodePostProcessor.cs       # Roslyn-based code cleanup and transformation
├── Analyzers/
│   └── SchemaAnalyzer.cs      # Detects shared vs variant-specific types
└── Generators/
    ├── SchemaGenerator.cs     # Orchestrates code generation
    ├── CleanTypeNameGenerator.cs      # Type name cleanup
    └── PascalCasePropertyNameGenerator.cs  # Property name conversion
```

**Flow:**

1. `SchemaAnalyzer` parses all schemas and categorizes types as shared, variant-specific, or conflicting
2. `SchemaGenerator` uses NJsonSchema to generate C# code with custom name generators
3. `CodePostProcessor` uses Roslyn syntax trees to clean up the generated code (seal classes, remove
   boilerplate, add interfaces)

## Dependencies

- [CommandLineParser](https://github.com/commandlineparser/commandline) - Command line argument parsing
- [NJsonSchema.CodeGeneration.CSharp](https://github.com/RicoSuter/NJsonSchema) - JSON Schema to C# code generation
- [Microsoft.CodeAnalysis.CSharp](https://github.com/dotnet/roslyn) - Roslyn C# syntax tree APIs for code formatting
- [Microsoft.Extensions.Logging](https://docs.microsoft.com/en-us/dotnet/core/extensions/logging) - Logging
  infrastructure
