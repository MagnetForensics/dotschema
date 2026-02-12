# DotSchema

A .NET tool that generates C# code from JSON schemas, with support for detecting shared and variant-specific types across multiple schema files.

## Features

- **JSON Schema to C# code generation** using NJsonSchema
- **Multi-schema analysis** to detect shared vs variant-specific types
- **Three generation modes:**
  - `All` - Generates shared types + variant-specific types for all variants
  - `Shared` - Generates only types that exist in all provided schemas
  - `Variant` - Generates only types unique to a specific variant
- **Automatic interface generation** for variant types
- **JetBrains code cleanup integration** (optional)

## Requirements

- .NET 8.0 SDK

## Installation

```bash
dotnet build
```

## Usage

```bash
dotnet run -- [options]
```

### Options

| Option | Short | Required | Description |
|--------|-------|----------|-------------|
| `--schemas` | `-s` | Yes | One or more JSON schema files to process |
| `--output` | `-o` | Yes | Output file path (Shared/Variant) or directory (All mode) |
| `--namespace` | `-n` | Yes | Namespace for generated types |
| `--mode` | `-m` | No | Generation mode: `All`, `Shared`, or `Variant` (default: `All`) |
| `--variant` | `-v` | No | Variant name for single-variant generation |
| `--no-interface` | | No | Skip generating the marker interface |
| `--no-cleanup` | | No | Skip running JetBrains code cleanup |

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

## Dependencies

- [CommandLineParser](https://github.com/commandlineparser/commandline) - Command line argument parsing
- [NJsonSchema.CodeGeneration.CSharp](https://github.com/RicoSuter/NJsonSchema) - JSON Schema to C# code generation
- [Microsoft.Extensions.Logging](https://docs.microsoft.com/en-us/dotnet/core/extensions/logging) - Logging infrastructure

