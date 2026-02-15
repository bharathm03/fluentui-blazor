# Fluent UI Blazor — Component & Example Data Extraction

## Context

We are building an AI-powered code generation platform that produces Blazor UI code from text descriptions. It already supports MudBlazor and Syncfusion. We are adding **Fluent UI Blazor** (`Microsoft.FluentUI.AspNetCore.Components`) as a new supported library.

The AI system needs two structured data files to understand Fluent UI components and generate correct code:
1. **A component catalog** — what components exist, what they're called, what they do
2. **An example corpus** — real working code examples showing how each component is used

These files will later be consumed by an AI agent system that selects relevant components and examples based on user requests, and injects them into LLM prompts as context.

## Your Task

Create a .NET console tool at `tools/DataExtractor/` that extracts data from this repository and produces two JSON files in `tools/DataExtractor/output/`. Do NOT modify any existing files in the repository.

The tool should reference the component library project at `src/Core/Microsoft.FluentUI.AspNetCore.Components.csproj` so it can use reflection on the component assembly.

---

## Output 1: `FluentComponents.json`

A catalog of every concrete, public Fluent UI component that a developer would use in Razor markup.

```json
[
  {
    "Title": "Button",
    "Description": "A button is a widget that enables users to trigger an action or event, such as submitting a form, opening a dialog, canceling an action, or performing a delete operation. `FluentButton` wraps the `fluent-button` element, a web component implementation of an HTML button element leveraging the Fluent UI design system. The `FluentButton` component supports several visual appearances (accent, lightweight, neutral, outline, stealth).",
    "Name": "FluentButton",
    "Aliases": null
  },
  {
    "Title": "Data Grid",
    "Description": "A component for displaying tabular data with sorting, filtering, and pagination.",
    "Name": "FluentDataGrid",
    "Aliases": null
  }
]
```

| Field | Type | Description |
|-------|------|-------------|
| `Title` | string | Human-readable name derived from the class name. Remove the `Fluent` prefix and insert spaces before uppercase letters. `FluentDataGrid` → `"Data Grid"`. |
| `Description` | string | The component's introductory description from its demo page (see "Where component descriptions come from" below). Convert from HTML to plain text / markdown. Empty string if no demo page exists. |
| `Name` | string | The full class name (e.g., `"FluentButton"`). |
| `Aliases` | string[]? | `null` for now. Reserved for future use. |

**What to include:** Only concrete (non-abstract), public component classes whose names start with `Fluent` and inherit from `ComponentBase`. The base class for most components is `FluentComponentBase` in namespace `Microsoft.FluentUI.AspNetCore.Components`.

**What to exclude:** Abstract base classes, internal types, non-component helper classes.

### Where component descriptions come from

Each component has a demo page at `examples/Demo/Shared/Pages/{ComponentName}/{ComponentName}Page.razor`. These pages have a consistent structure — introductory HTML content sits between the `<h1>` heading and the `<h2 id="example">Examples</h2>` marker. This intro section is the component's description.

For example, `ButtonPage.razor`:

```razor
<h1>Button</h1>

<p>
    As defined by the <a href="https://www.w3.org/WAI/ARIA/apg/patterns/button/">W3C</a>:
</p>
<blockquote>
    <p>
        A button is a widget that enables users to trigger an action or event,
        such as submitting a form, opening a dialog, canceling an action, or
        performing a delete operation.
    </p>
</blockquote>
<p>
    <code>&lt;FluentButton&gt;</code> wraps the <code>&lt;fluent-button&gt;</code>
    element, a web component implementation of an HTML button element leveraging the
    Fluent UI design system. The <code>&lt;FluentButton&gt;</code> component supports
    several visual appearances (accent, lightweight, neutral, outline, stealth).
</p>

<h2 id="example">Examples</h2>
```

Extract the HTML between `<h1>...</h1>` and `<h2 id="example">`, then convert it to clean markdown/plain text (strip HTML tags, convert `<code>` to backticks, convert `<a>` to markdown links, convert `<blockquote>` to quoted text). Skip `<CodeSnippet>` blocks if present.

If a component has no matching demo page, fall back to the XML doc `<summary>` from the assembly. If neither exists, use empty string.

---

## Output 2: `FluentExampleUsageDoc.json`

A corpus of working code examples extracted from this repo's demo application.

```json
[
  {
    "ComponentName": "FluentButton",
    "Title": "ButtonDefault",
    "Key": "BUTTON01",
    "Summary": "",
    "CodeFiles": [
      {
        "Code": "<FluentStack HorizontalGap=\"10\">\n    <FluentButton>Button</FluentButton>\n</FluentStack>",
        "Name": "ButtonDefault.razor",
        "IsMain": false,
        "Hidden": false,
        "Type": 1
      }
    ],
    "Description": "",
    "OriginalDescription": "Simple button examples showing different appearances."
  }
]
```

| Field | Type | Description |
|-------|------|-------------|
| `ComponentName` | string | The Fluent UI class name this example demonstrates. Derive from the parent folder: folder `Button` → `"FluentButton"`. |
| `Title` | string | The example's file name without extension (e.g., `"ButtonDefault"`). |
| `Key` | string | Unique identifier. Format: `{FOLDERNAME_UPPERCASED}{INDEX:D2}`. E.g., `"BUTTON01"`, `"DATAGRID03"`. Sequential within each component. |
| `Summary` | string | Empty string. Will be LLM-generated in a later pipeline step. |
| `CodeFiles` | array | All files belonging to this example (see below). |
| `Description` | string | Empty string. Will be LLM-generated later. |
| `OriginalDescription` | string? | Author's description from the demo page, if available. `null` if not found. |

**CodeFile schema:**

| Field | Type | Description |
|-------|------|-------------|
| `Code` | string | Full raw file content. |
| `Name` | string | File name (e.g., `"ButtonDefault.razor"`). |
| `IsMain` | bool | `false` |
| `Hidden` | bool | `false` |
| `Type` | int | `1` |

### Where the examples live

```
examples/Demo/Shared/Pages/{ComponentName}/Examples/*.razor    ← example code
examples/Demo/Shared/Pages/{ComponentName}/{ComponentName}Page.razor  ← demo page with descriptions
```

Each component folder (e.g., `Button/`) contains an `Examples/` subfolder with individual `.razor` example files. Companion files (`.razor.cs`, `.razor.css`) for the same example should be grouped as additional entries in that example's `CodeFiles` array.

### Where `OriginalDescription` comes from

Each component's `*Page.razor` uses `<DemoSection>` components to organize examples:

```razor
<DemoSection Title="Simple button examples" Component="typeof(ButtonDefault)">
    <Description>Shows different button appearances and states.</Description>
</DemoSection>
```

Match each example to its `<DemoSection>` by the `Component="typeof(...)"` reference, and extract the `<Description>` text content. This is best-effort — if parsing fails or no description exists, use `null`.

### Edge cases

- Some folders (e.g., `Home`, `Lab`) are infrastructure, not component demos. Skip folders that have no `Examples/` subfolder or don't map to a real Fluent UI component.
- Some folders like `DateTimes` may contain examples for multiple components (`FluentDatePicker`, `FluentTimePicker`). Use the example file content or naming to determine the correct `ComponentName`.
- Files with `_` in the name (e.g., `DataGridTypical_Helper.razor`) are sub-files — group them with the base name example.

---

## Constraints

- **Target framework:** `net9.0` (matches the repo)
- **No modifications** to existing repository code
- **PascalCase** property naming in JSON output (no camelCase)
- **Indented JSON** for readability
- Accept the repo root path as a command-line argument, defaulting to current directory

---

## Acceptance Criteria

| Check | Requirement |
|-------|-------------|
| Build | `dotnet build tools/DataExtractor/DataExtractor.csproj` succeeds |
| Run | `dotnet run --project tools/DataExtractor -- .` produces both JSON files |
| Components count | `FluentComponents.json` contains 50+ entries |
| Examples count | `FluentExampleUsageDoc.json` contains 100+ entries |
| Component names | Every entry in `FluentComponents.json` has a non-empty `Name` starting with `"Fluent"` |
| Descriptions | Majority of entries in `FluentComponents.json` have a non-empty `Description` sourced from their demo page |
| Example code | Every entry in `FluentExampleUsageDoc.json` has non-empty `Code` in at least one CodeFile |
| Valid JSON | Both files parse without errors |

Build the tool, run it, and verify the output. Commit the tool and the generated JSON files.
