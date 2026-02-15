# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Microsoft Fluent UI Blazor — a Razor class library of 60+ Blazor components implementing Microsoft's Fluent Design System. Targets .NET 8, 9, and 10 simultaneously. MIT licensed, maintained by Microsoft.

## Build Commands

```bash
# Build entire solution
dotnet build

# Build just the core library
dotnet build src/Core/Microsoft.FluentUI.AspNetCore.Components.csproj

# Run all tests
dotnet test tests/Core/Microsoft.FluentUI.AspNetCore.Components.Tests.csproj

# Run a single test by name
dotnet test tests/Core/Microsoft.FluentUI.AspNetCore.Components.Tests.csproj --filter "FullyQualifiedName~FluentButtonTests.FluentButton_Default"

# Run tests with code coverage
dotnet test tests/Core/Microsoft.FluentUI.AspNetCore.Components.Tests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

# Run the demo app
dotnet run --project examples/Demo/Server/FluentUI.Demo.Server.csproj
```

## Prerequisites

- .NET SDK (8.0/9.0/10.0)
- Node.js v22.x+ (for building TypeScript assets in `src/Core.Assets/`)

## Solution Structure

- **`src/Core/`** — Main component library (`Microsoft.FluentUI.AspNetCore.Components`). All components live under `Components/` with collocated files (.razor, .razor.cs, .razor.css, .razor.js).
- **`src/Core.Assets/`** — TypeScript/JS assets built with esbuild, bundled into the core library's wwwroot.
- **`src/Extensions/`** — DataGrid adapters (EntityFramework, OData) and a DesignToken code generator.
- **`src/Templates/`** — dotnet new project templates (Blazor Web, WASM, MAUI, Aspire).
- **`tests/Core/`** — Unit tests using bUnit + xUnit. Tests organized by component folder (e.g., `tests/Core/Button/`).
- **`examples/Demo/`** — Full demo app split into Server, Client (WASM), and Shared projects.

## Architecture

### Component Pattern

All components inherit from `FluentComponentBase` (in `src/Core/Components/Base/`), which extends Blazor's `ComponentBase` and provides: `Id`, `Class`, `Style`, `Data`, `ParentReference`, `AdditionalAttributes`, and an `Element` reference.

Components use the collocated file pattern:
- `ComponentName.razor` — Markup
- `ComponentName.razor.cs` — Code-behind (partial class)
- `ComponentName.razor.css` — Scoped CSS (optional)
- `ComponentName.razor.js` — JS interop (optional)

All components share the namespace `Microsoft.FluentUI.AspNetCore.Components`.

### Service Registration

Components are registered via `builder.Services.AddFluentUIComponents()`. This configures services for Toasts, Dialogs, Tooltips, MessageBars, and Menus. Lifetime is configurable via `LibraryConfiguration`.

### Design Tokens

The `src/Core/DesignTokens/` directory contains the design token system (Swatch, Reference, DesignToken.razor) for Fluent Design System customization.

## Testing Conventions

Tests use **bUnit** with **xUnit**. Test classes extend `TestContext` and set up JSInterop in loose mode:

```csharp
public partial class FluentButtonTests : TestContext
{
    public FluentButtonTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(LibraryConfiguration.ForUnitTests);
    }
}
```

### Snapshot Testing

Tests call `cut.Verify()` which compares rendered HTML against `.verified.html` files colocated with the test class. On mismatch, a `.received.html` file is written for comparison. To update snapshots after intentional changes, use `FluentAssert.Options.UpdateVerifiedFiles = true` or manually replace the `.verified.html` content.

Deterministic IDs are ensured with `using var id = Identifier.SequentialContext();` in tests.

## Build Configuration

- **Centralized package versions**: `Directory.Packages.props` (ManagePackageVersionsCentrally)
- **Shared build props**: `Directory.Build.props` — enables nullable, latest LangVersion, code style enforcement, .NET analyzers
- **Build output**: `artifacts/` directory
- **NuGet feed**: Azure DevOps public feed (configured in `NuGet.config`)

## Data Extraction Tool

The `tools/DataExtractor/` console app extracts component metadata and code examples from this repository into JSON files for the Instruct UI AI code-generation pipeline.

```bash
# Build the tool
dotnet build tools/DataExtractor/DataExtractor.csproj

# Run from repo root (pass repo root path as argument)
dotnet run --project tools/DataExtractor -- .
```

**Output** (written to `tools/DataExtractor/output/`):
- `FluentComponents.json` — Component catalog (Name, Title, Description, Aliases)
- `FluentExampleUsageDoc.json` — Example corpus (ComponentName, Key, CodeFiles, OriginalDescription)

The tool uses reflection on the core library assembly to discover components, then parses demo pages in `examples/Demo/Shared/Pages/` for descriptions and code examples. XML doc summaries are prefixed to page descriptions when both are available.

## PR Conventions

- PR title format: `[ComponentName] Description` (no period, omit "Fluent" prefix)
- Use `fix #issue` syntax to auto-close related issues
- Include before/after screenshots when applicable
