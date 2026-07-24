# ScheduleEditor

**English** | [简体中文](README.zh-CN.md)

A reusable Avalonia component for visually configuring and running scheduled tasks inside the host application process. Ordinary users can configure schedules through a graphical editor without learning Cron, while advanced applications may expose a custom Cron mode when needed.

- NuGet package: `ScheduleEditor`
- Authors: `vincent, chatgpt`
- GitHub: `https://github.com/hupo376787/ScheduleEditor`

## Features

- .NET 10
- Avalonia 12.1
- FluentScheduler 6.0.0
- NCrontab 3.4.0
- CommunityToolkit.Mvvm 8.4.2
- Run every N seconds
- Run every N minutes
- Run every N hours
- Run daily at a fixed time
- Run weekly on selected weekdays at a fixed time
- Run monthly on a selected day and time
- Custom 5-field or 6-field Cron expressions
- Individual visibility switches for every repeat mode
- Daily, weekly, and monthly modes are visible by default
- Enable, disable, save, cancel, and next-run preview
- Horizontally and vertically centered Save and Cancel button content
- Built-in JSON persistence implementation
- Automatic configuration loading and scheduler restoration on startup
- Replaceable persistence through SQLite or a host-defined store
- Overlap prevention with `SemaphoreSlim`
- Built-in Simplified Chinese and English
- Complete or partial external JSON language packs
- HotAvalonia Hot Reload in Debug builds
- `AvaloniaUI.DiagnosticsSupport 2.2.3` in the Demo
- Suitable for Windows, macOS, and Linux desktop applications

> The scheduler runs only while the host application process is running. It does not execute after the application exits or while the computer is shut down or asleep.

## Architecture

```text
ScheduleEditor
    Editing, display, validation, and localization
                   ↓
ScheduleEditorViewModel
                   ↓
ScheduleManager
    Load, save, restore, and apply schedules
        ↓                      ↓
IScheduleStore            FluentScheduleService
        ↓
JsonScheduleStore / SQLite / host-defined persistence
```

The control does not choose a storage location by itself. The library provides `JsonScheduleStore`, while the host application decides the actual file path.

## Project layout

```text
src/ScheduleEditor
  Controls/
    ScheduleEditor.axaml
    ScheduleEditor.axaml.cs
  Localization/
    IScheduleLocalizationService.cs
    ScheduleLocalizationService.cs
    ScheduleLanguagePack.cs
    ScheduleLanguageInfo.cs
    ScheduleTextKeys.cs
  Models/
    ScheduleOptions.cs
    ScheduleRepeatType.cs
  Services/
    IScheduleStore.cs
    JsonScheduleStore.cs
    ScheduleManager.cs
    IFluentScheduleService.cs
    FluentScheduleService.cs
    ScheduleCalculator.cs
    ScheduleOptionsValidator.cs
    ScheduleExecutionEventArgs.cs
  ViewModels/
    ScheduleEditorViewModel.cs
    WeekDayItemViewModel.cs

demo/ScheduleEditor.Demo
  Complete integration example, JSON restoration, language switching,
  an external Japanese language pack, and execution logs
```

## Restore and run

### Recommended commands

After installing the .NET 10 SDK, run these commands from the solution root:

```powershell
dotnet restore .\ScheduleEditor.sln
dotnet run --project .\demo\ScheduleEditor.Demo
```

You can also open the following solution in Visual Studio or Rider:

```text
ScheduleEditor.sln
```

### NuGet access-denied errors

The solution includes `NuGet.Config` and `restore-local.cmd`. They place restored packages inside the solution at:

```text
.nuget\packages
```

Close Visual Studio and run:

```text
restore-local.cmd
```

Or run the commands manually:

```powershell
dotnet restore .\ScheduleEditor.sln `
  --packages .\.nuget\packages `
  --force `
  --no-cache

dotnet build .\ScheduleEditor.sln `
  -c Debug `
  --no-restore
```

When the first error says that NuGet restore failed because access to a path was denied, later errors about a missing `NCrontab` namespace or missing component DLL are normally cascading errors caused by the failed restore.

# Integrating into application A

## 1. Reference the component project

```xml
<ItemGroup>
  <ProjectReference Include="..\ScheduleEditor\ScheduleEditor.csproj" />
</ItemGroup>
```

Install the stable NuGet package directly:

```powershell
dotnet add package ScheduleEditor --version 1.0.0
```

Or add this to the project file:

```xml
<ItemGroup>
  <PackageReference Include="ScheduleEditor" Version="1.0.0" />
</ItemGroup>
```

The NuGet package ID, assembly name, and root namespace are all `ScheduleEditor`. Use `ScheduleEditor.*` in C# and `using:ScheduleEditor.*` in AXAML.

## 2. Add the editor to the UI

Daily, weekly, and monthly modes are visible by default:

```xml
<Window
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:controls="using:ScheduleEditor.Controls">

  <controls:ScheduleEditor
      DataContext="{Binding ScheduleEditor}" />
</Window>
```

## 3. Control which repeat modes are visible

```xml
<controls:ScheduleEditor
    DataContext="{Binding ScheduleEditor}"
    ShowEverySeconds="True"
    ShowEveryMinutes="True"
    ShowEveryHours="True"
    ShowDaily="True"
    ShowWeekly="True"
    ShowMonthly="True"
    ShowCron="True" />
```

Default values:

| Property | Default | Mode |
|---|---:|---|
| `ShowEverySeconds` | `False` | Every N seconds |
| `ShowEveryMinutes` | `False` | Every N minutes |
| `ShowEveryHours` | `False` | Every N hours |
| `ShowDaily` | `True` | Daily |
| `ShowWeekly` | `True` | Weekly |
| `ShowMonthly` | `True` | Monthly |
| `ShowCron` | `False` | Custom Cron |

These are Avalonia `StyledProperty` values and may be bound to the host ViewModel:

```xml
<controls:ScheduleEditor
    DataContext="{Binding ScheduleEditor}"
    ShowCron="{Binding AllowAdvancedCron}"
    ShowEverySeconds="{Binding AllowSecondInterval}" />
```

If the currently selected mode becomes hidden, the editor automatically selects the first visible mode. The host must expose at least one repeat mode.

## 4. Create persistence, the manager, and the editor ViewModel

```csharp
using ScheduleEditor.Localization;
using ScheduleEditor.Models;
using ScheduleEditor.Services;
using ScheduleEditor.ViewModels;

public sealed class MainViewModel : IDisposable
{
    private readonly JsonScheduleStore _store;
    private readonly ScheduleManager _scheduleManager;
    private readonly ScheduleLocalizationService _localization;

    public MainViewModel()
    {
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ProgramA",
            "schedule.json");

        // The library supplies JSON persistence; the host chooses the path.
        _store = new JsonScheduleStore(settingsPath);

        // ExecuteScheduledTaskAsync is called when the schedule fires.
        _scheduleManager = new ScheduleManager(
            _store,
            ExecuteScheduledTaskAsync);

        // Pass zh-CN, en-US, or another previously registered culture.
        _localization = new ScheduleLocalizationService("en-US");

        ScheduleEditor = new ScheduleEditorViewModel(_localization)
        {
            // Save validates, writes JSON, stops the old rule, and applies the new one.
            SaveHandler = options =>
                _scheduleManager.SaveAndApplyAsync(options),

            CancelHandler = () => Task.CompletedTask
        };
    }

    public ScheduleEditorViewModel ScheduleEditor { get; }

    public string ScheduleFilePath => _store.FilePath;

    public bool IsScheduleRunning => _scheduleManager.IsRunning;

    public DateTimeOffset? NextRun => _scheduleManager.NextRun;

    private async Task ExecuteScheduledTaskAsync(
        CancellationToken cancellationToken)
    {
        // Replace this with application A's actual work:
        // collection, export, upload, cleanup, and so on.
        await Task.Delay(1000, cancellationToken);
    }

    public void Dispose()
    {
        _scheduleManager.Dispose();
    }
}
```

Application A does not need to create FluentScheduler `Schedule` instances directly. `ScheduleManager` and `FluentScheduleService` encapsulate creation, stopping, rescheduling, and overlap prevention.

## 5. Load JSON and restore the scheduler on startup

```csharp
public async Task InitializeAsync()
{
    var options = await _scheduleManager.InitializeAsync(
        new ScheduleOptions
        {
            // Defaults used when the application runs for the first time.
            IsEnabled = false,
            RepeatType = ScheduleRepeatType.Daily,
            ExecutionTime = new TimeSpan(9, 30, 0)
        });

    // Load the actual restored or default configuration into the editor.
    ScheduleEditor.Load(options);
}
```

`InitializeAsync()` performs the following work:

1. Loads configuration from `IScheduleStore`.
2. Uses the supplied defaults when no stored configuration exists.
3. Validates and normalizes the configuration.
4. Restores FluentScheduler when the configuration is enabled.
5. Returns the configuration that is actually in effect.

Window lifecycle example:

```csharp
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        Opened += async (_, _) =>
            await _viewModel.InitializeAsync();

        Closing += (_, _) =>
            _viewModel.Dispose();
    }
}
```

# Supported schedule configurations

## Every 10 seconds

```csharp
var options = new ScheduleOptions
{
    IsEnabled = true,
    RepeatType = ScheduleRepeatType.EverySeconds,
    Interval = 10
};
```

## Every 15 minutes

```csharp
var options = new ScheduleOptions
{
    IsEnabled = true,
    RepeatType = ScheduleRepeatType.EveryMinutes,
    Interval = 15
};
```

## Every 2 hours

```csharp
var options = new ScheduleOptions
{
    IsEnabled = true,
    RepeatType = ScheduleRepeatType.EveryHours,
    Interval = 2
};
```

## Daily at 09:30

```csharp
var options = new ScheduleOptions
{
    IsEnabled = true,
    RepeatType = ScheduleRepeatType.Daily,
    ExecutionTime = new TimeSpan(9, 30, 0)
};
```

## Monday, Wednesday, and Friday at 09:30

```csharp
var options = new ScheduleOptions
{
    IsEnabled = true,
    RepeatType = ScheduleRepeatType.Weekly,
    ExecutionTime = new TimeSpan(9, 30, 0),
    WeekDays =
    [
        DayOfWeek.Monday,
        DayOfWeek.Wednesday,
        DayOfWeek.Friday
    ]
};
```

## The 15th day of every month at 09:30

```csharp
var options = new ScheduleOptions
{
    IsEnabled = true,
    RepeatType = ScheduleRepeatType.Monthly,
    DayOfMonth = 15,
    ExecutionTime = new TimeSpan(9, 30, 0)
};
```

If a month does not contain the selected date, such as February 30, that month is skipped.

## Custom Cron

```csharp
var options = new ScheduleOptions
{
    IsEnabled = true,
    RepeatType = ScheduleRepeatType.Cron,
    CronExpression = "*/10 * * * * *"
};
```

Supported formats:

```text
5 fields: minute hour day month weekday
6 fields: second minute hour day month weekday
```

Examples:

```text
0 9 * * *        Daily at 09:00
0 30 9 * * *     Daily at 09:30:00
*/10 * * * * *   Every 10 seconds
```

NCrontab is used to validate Cron syntax and calculate the next occurrence. Applications intended for ordinary users can keep Cron mode hidden.

## Save and apply a configuration directly

```csharp
await _scheduleManager.SaveAndApplyAsync(options);
```

The method persists the configuration first and applies the new schedule only after persistence succeeds.

# JSON persistence

Example for Monday, Wednesday, and Friday at 09:30:

```json
{
  "IsEnabled": true,
  "RepeatType": "Weekly",
  "Interval": 1,
  "ExecutionTime": "09:30:00",
  "WeekDays": [
    "Monday",
    "Wednesday",
    "Friday"
  ],
  "DayOfMonth": 1,
  "CronExpression": "0 9 * * *"
}
```

`JsonScheduleStore`:

- serializes enums as strings;
- creates parent directories automatically;
- writes a temporary file and then replaces the destination atomically;
- accepts custom `JsonSerializerOptions`;
- leaves the storage path under host application control.

The JSON remains after the process exits. The application must call `ScheduleManager.InitializeAsync()` on its next startup to restore scheduling.

# SQLite or another persistence provider

Implement `IScheduleStore`:

```csharp
public sealed class SqliteScheduleStore : IScheduleStore
{
    public Task<ScheduleOptions?> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        // Load from SQLite.
        throw new NotImplementedException();
    }

    public Task SaveAsync(
        ScheduleOptions options,
        CancellationToken cancellationToken = default)
    {
        // Save to SQLite.
        throw new NotImplementedException();
    }
}
```

Replace only the store object:

```csharp
IScheduleStore store = new SqliteScheduleStore();

_scheduleManager = new ScheduleManager(
    store,
    ExecuteScheduledTaskAsync);
```

No editor or scheduler code needs to change.

# Localization

## Built-in languages

The component includes:

- `zh-CN`: Simplified Chinese
- `en-US`: English

The host application should create and reuse one `ScheduleLocalizationService` instance, then pass that same instance to `ScheduleEditorViewModel`:

```csharp
private readonly ScheduleLocalizationService _localization;

public MainViewModel()
{
    _localization = new ScheduleLocalizationService("en-US");

    ScheduleEditor = new ScheduleEditorViewModel(_localization)
    {
        SaveHandler = options =>
            _scheduleManager.SaveAndApplyAsync(options)
    };
}

public ScheduleEditorViewModel ScheduleEditor { get; }
```

Do not create separate localization-service instances for the language selector and the editor. A language pack registered in one instance is not visible in another instance.

Switch at runtime:

```csharp
_localization.SetCulture("zh-CN");
_localization.SetCulture("en-US");
```

The editor listens for `LanguageChanged` and refreshes its text immediately.

## Passing an external JSON language pack from application A

The control does not automatically scan for external language packs. The host application must:

1. add the JSON file to application A;
2. copy it to the output directory during build;
3. load it through the localization service at startup.

### Step 1: add the language file to application A

Example layout:

```text
ProgramA/
  Languages/
    ja-JP.json
```

Complete language-pack format:

```json
{
  "culture": "ja-JP",
  "displayName": "日本語",
  "fallbackCulture": "en-US",
  "strings": {
    "Title": "自動実行",
    "Enabled": "有効",
    "Save": "保存",
    "Cancel": "キャンセル"
  }
}
```

Field reference:

| Field | Required | Description |
|---|---:|---|
| `culture` | Yes | Culture identifier such as `ja-JP` or `de-DE` |
| `displayName` | No | Name shown in a language selector; culture is used when omitted |
| `fallbackCulture` | No | Language used for missing strings; new packs default to `en-US` |
| `strings` | No | Text-key dictionary; partial dictionaries are supported |

JSON property names are case-insensitive. Trailing commas and comments are accepted. UTF-8 is recommended.

### Step 2: copy the file to the output directory

Add this to application A's `.csproj`:

```xml
<ItemGroup>
  <None Update="Languages\*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

The published output should contain:

```text
application output/
  ProgramA.exe
  Languages/
    ja-JP.json
```

### Step 3: load it at startup and pass it to the component

```csharp
public async Task InitializeAsync()
{
    var languageFile = Path.Combine(
        AppContext.BaseDirectory,
        "Languages",
        "ja-JP.json");

    await _localization.AddOrUpdateLanguagePackFromFileAsync(
        languageFile,
        setCurrent: true);

    var schedule = await _scheduleManager.InitializeAsync();
    ScheduleEditor.Load(schedule);
}
```

Continue using the same `_localization` instance that was passed to `ScheduleEditorViewModel`. That shared instance is the connection through which the host passes languages into the component.

Use `AppContext.BaseDirectory` instead of relying on the current working directory, which may differ when the application is launched from a shortcut, a service, or another IDE.

`setCurrent` behavior:

- `false`: register the language without changing the UI;
- `true`: register the language and switch to it immediately.

The editor ViewModel exposes the same operation:

```csharp
await ScheduleEditor.AddOrUpdateLanguagePackFromFileAsync(
    languageFile,
    setCurrent: true);
```

## Passing a JSON string directly

This is useful when the pack comes from a database, an HTTP response, an embedded resource, or the host's own settings system:

```csharp
var languageJson = await File.ReadAllTextAsync(languageFile);

_localization.AddOrUpdateLanguagePackFromJson(
    languageJson,
    setCurrent: true);
```

When the host has a `Stream`, read it into a string first:

```csharp
using var reader = new StreamReader(stream);
var languageJson = await reader.ReadToEndAsync(cancellationToken);

_localization.AddOrUpdateLanguagePackFromJson(
    languageJson,
    setCurrent: true);
```

## Passing an object directly

A host that does not use JSON files may construct a `ScheduleLanguagePack` directly:

```csharp
_localization.AddOrUpdateLanguagePack(
    new ScheduleLanguagePack
    {
        Culture = "de-DE",
        DisplayName = "Deutsch",
        FallbackCulture = "en-US",
        Strings = new Dictionary<string, string>
        {
            [ScheduleTextKeys.Title] = "Automatische Ausführung",
            [ScheduleTextKeys.Enabled] = "Aktiviert",
            [ScheduleTextKeys.Save] = "Speichern",
            [ScheduleTextKeys.Cancel] = "Abbrechen"
        }
    },
    setCurrent: true);
```

This is suitable when application A already has a resource system and converts its resources into a dictionary at runtime.

## Overriding selected built-in strings

A partial override JSON document is a **flat key/value object**, which is different from the complete language-pack format:

```json
{
  "Title": "Task Automation",
  "Save": "Apply Settings",
  "Cancel": "Discard Changes"
}
```

Apply it to built-in English:

```csharp
var overrideJson = await File.ReadAllTextAsync(
    Path.Combine(
        AppContext.BaseDirectory,
        "Languages",
        "en-US.override.json"));

_localization.AddOrUpdateLanguageOverridesFromJson(
    culture: "en-US",
    json: overrideJson,
    displayName: "Customer English",
    fallbackCulture: "en-US",
    setCurrent: true);
```

Registering the same culture again merges its `strings`: new keys replace existing values, while omitted keys keep their previous values. Customers therefore do not need to copy the complete built-in pack.

## Loading multiple customer packs at startup

```csharp
public async Task LoadCustomerLanguagesAsync(
    CancellationToken cancellationToken = default)
{
    var directory = Path.Combine(
        AppContext.BaseDirectory,
        "Languages");

    if (!Directory.Exists(directory))
        return;

    foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
    {
        await _localization.AddOrUpdateLanguagePackFromFileAsync(
            file,
            setCurrent: false,
            cancellationToken);
    }

    // Switch only after every pack has been registered.
    _localization.SetCulture("ja-JP");
}
```

When the directory also contains partial override files, use a separate suffix such as `*.override.json` and load those through `AddOrUpdateLanguageOverridesFromJson()`, because the two JSON structures are different.

## Language selector

```csharp
public IReadOnlyList<ScheduleLanguageInfo> Languages =>
    _localization.AvailableLanguages;

public void ChangeLanguage(ScheduleLanguageInfo language)
{
    _localization.SetCulture(language.Culture);
}
```

Avalonia example:

```xml
<ComboBox
    ItemsSource="{Binding Languages}"
    SelectedItem="{Binding SelectedLanguage}">
  <ComboBox.ItemTemplate>
    <DataTemplate>
      <TextBlock Text="{Binding DisplayName}" />
    </DataTemplate>
  </ComboBox.ItemTemplate>
</ComboBox>
```

Registering a language raises `LanguagesChanged`. A host that exposes languages through an `ObservableCollection` should refresh it from that event. When language packs are loaded on a background thread, marshal UI-collection updates through `Dispatcher.UIThread.Post`.

## Persisting the customer's selected language

Language-pack registration and the current culture exist only in memory:

- they are not written into `schedule.json`;
- external packs are not remembered after the process exits;
- the host must register external packs again on every startup;
- the selected culture, such as `ja-JP`, must be persisted separately when required.

Example:

```csharp
// Persist the user's choice.
await File.WriteAllTextAsync(
    cultureSettingsPath,
    _localization.CurrentCulture);

// On the next startup, register packs before restoring the selection.
await LoadCustomerLanguagesAsync();

if (File.Exists(cultureSettingsPath))
{
    var savedCulture = (
        await File.ReadAllTextAsync(cultureSettingsPath)).Trim();

    if (_localization.AvailableLanguages.Any(language =>
        language.Culture.Equals(
            savedCulture,
            StringComparison.OrdinalIgnoreCase)))
    {
        _localization.SetCulture(savedCulture);
    }
}
```

The requested culture must be registered before calling `SetCulture()`, otherwise `KeyNotFoundException` is thrown.

## Fallback and forward compatibility

Missing text is resolved in this order:

```text
current culture → fallbackCulture → en-US → text key name
```

When a later component version adds new text keys, older customer packs therefore continue to work. New untranslated keys fall back to English.

Only text inside `ScheduleEditor` is localized by this library. Window titles, logs, buttons, and status text owned by application A remain the host application's responsibility.

## Format-placeholder rules

The following keys contain placeholders that customer translations must preserve:

| Key | Required placeholders |
|---|---|
| `SummaryEverySeconds` | `{0}` |
| `SummaryEveryMinutes` | `{0}` |
| `SummaryEveryHours` | `{0}` |
| `SummaryDaily` | `{0}` |
| `SummaryWeekly` | `{0}`, `{1}` |
| `SummaryMonthly` | `{0}`, `{1}` |
| `SummaryCron` | `{0}` |
| `ErrorInvalidCron` | `{0}` |
| `ErrorSaveFailed` | `{0}` |

`DateTimeFormat` uses a .NET date/time format string, for example:

```json
{
  "DateTimeFormat": "yyyy-MM-dd ddd HH:mm:ss"
}
```

An invalid customer placeholder does not crash the editor, but the untranslated template may be shown as-is.

## Available text keys

The complete key list is defined in:

```text
src/ScheduleEditor/Localization/ScheduleTextKeys.cs
```

A complete external-pack example that can be copied is available at:

```text
demo/ScheduleEditor.Demo/Languages/ja-JP.json
```

Key groups cover:

- title, enabled state, and repeat modes;
- interval, time, weekdays, monthly date, and Cron fields;
- summaries and next-run feedback;
- save feedback and validation errors;
- date format and list separator;
- short and long weekday names.

## Troubleshooting

### The language file cannot be found

Verify `CopyToOutputDirectory` and print the resolved path:

```csharp
Console.WriteLine(languageFile);
```

### `SetCulture("ja-JP")` reports that the language is not registered

Call `AddOrUpdateLanguagePack...` first, or pass `setCurrent: true` while loading.

### Registration succeeds but the editor text does not change

Verify that `ScheduleEditorViewModel` and the external loading code share the same `ScheduleLocalizationService` instance.

### Only some strings are translated

This is supported. Missing keys follow the fallback chain. To provide a complete translation, use `ScheduleTextKeys.cs` or the Demo Japanese pack as the template.

# Execution events

```csharp
_scheduleManager.ExecutionStarted += (_, e) =>
{
    Console.WriteLine($"Started: {e.StartedAt}");
};

_scheduleManager.ExecutionCompleted += (_, e) =>
{
    Console.WriteLine($"Completed, duration: {e.Duration}");
};

_scheduleManager.ExecutionFailed += (_, e) =>
{
    Console.WriteLine($"Failed: {e.Exception?.Message}");
};

_scheduleManager.ExecutionSkipped += (_, _) =>
{
    Console.WriteLine("The previous run is still active; this occurrence was skipped.");
};
```

`FluentScheduleService` uses a non-blocking `SemaphoreSlim`. When a previous run is still active, the new occurrence is not executed concurrently and `ExecutionSkipped` is raised.

# Hot Reload

The component and Demo projects contain the following Debug-only configuration:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
  <HotAvaloniaMode>Balanced</HotAvaloniaMode>
  <HotAvaloniaHotkey>Alt+F5</HotAvaloniaHotkey>
</PropertyGroup>

<ItemGroup Condition="'$(Configuration)' == 'Debug'">
  <PackageReference Include="Avalonia.Markup.Xaml.Loader"
                    Version="12.1.0"
                    PrivateAssets="All"
                    Publish="True" />
  <PackageReference Include="HotAvalonia"
                    Version="3.1.4"
                    PrivateAssets="All"
                    Publish="True" />
</ItemGroup>
```

Usage:

1. Start the Demo with the Debug configuration.
2. Edit and save an `.axaml` file.
3. HotAvalonia reloads the UI automatically.
4. Press `Alt + F5` to force a reload when necessary.

These dependencies are referenced only in Debug builds and do not enter the normal Release dependency graph.

# Avalonia Developer Tools

The Demo references the following package in Debug builds:

```xml
<PackageReference Include="AvaloniaUI.DiagnosticsSupport"
                  Version="2.2.3" />
```

`App.Initialize()` contains:

```csharp
#if DEBUG
this.AttachDeveloperTools();
#endif
```

The deprecated `Avalonia.Diagnostics` package has been removed.

# Scheduling implementation notes

- Every-N-second, minute, and hour modes use FluentScheduler interval rules directly.
- Daily mode uses a fixed daily time.
- Weekly and monthly modes wake once per day at the selected time, then the component verifies the weekday or day of month.
- Custom Cron expressions are passed to FluentScheduler.
- `NextRun` uses `ScheduleCalculator` for the true weekly and monthly occurrence.
- Every configuration passes through `ScheduleOptionsValidator` before being applied.
- `ScheduleOptions.Normalize()` removes seconds from fixed execution times, sorts weekdays, and trims Cron whitespace.

# Important behavior

- Scheduling works only while the application process is running.
- No task runs after the application exits.
- No task runs while the computer is shut down.
- Missed executions during system sleep are not replayed automatically.
- Call `ScheduleManager.InitializeAsync()` after every application startup.
- Interval modes begin counting when the configuration is applied; they are not aligned to wall-clock boundaries.
- One `ScheduleManager` manages one task. For multiple independent tasks, use a separate storage path and manager for each task.
- Business jobs should observe the supplied `CancellationToken`.
- Do not block the Avalonia UI thread from a scheduled job. Use `Dispatcher.UIThread.Post` when UI updates are required.
- External language packs and the current culture are not persisted by `JsonScheduleStore`; register packs on every startup and persist the selected culture separately when needed.
- `ScheduleEditorViewModel` implements `IDisposable`; dispose it with the window or host ViewModel to unsubscribe localization events.

# Packing and publishing to NuGet

The default package metadata is:

```text
PackageId: ScheduleEditor
Version:   1.0.0
```

Run this command from the solution root:

```powershell
.\pack-release.ps1
```

Generated files:

```text
artifacts/ScheduleEditor.1.0.0.nupkg
artifacts/ScheduleEditor.1.0.0.snupkg
```

Verify package contents before publishing:

```powershell
.\verify-nupkg.ps1
```

Set the NuGet.org API key and publish:

```powershell
$env:NUGET_API_KEY = "your API key"
.\push-nuget.ps1
Remove-Item Env:NUGET_API_KEY
```

NuGet.org does not allow an existing package version to be overwritten. After publishing `1.0.0`, use `1.0.1` for a bug-fix release.
