# AvaloniaScheduleEditor

[English](README.en-US.md) | **简体中文**

一个面向普通用户的 Avalonia 定时任务编辑与进程内调度组件。用户可以通过图形界面设置重复规则，不需要理解 Cron；需要高级规则时，也可以选择开放自定义 Cron 输入。

## 功能

- .NET 10
- Avalonia 12.1
- FluentScheduler 6.0.0
- NCrontab 3.4.0
- CommunityToolkit.Mvvm 8.4.2
- 每 N 秒执行
- 每 N 分钟执行
- 每 N 小时执行
- 每天固定时间执行
- 每周选择多个星期并在固定时间执行
- 每月指定日期和时间执行
- 自定义 5 段或 6 段 Cron
- 每种重复方式可单独控制是否显示
- 默认只显示每天、每周、每月
- 启用、停用、保存、取消和下次执行预览
- 保存和取消按钮文本水平、垂直居中
- 组件库提供 JSON 持久化实现
- 程序启动时读取配置并恢复调度
- 可替换为 SQLite 或宿主自己的配置系统
- `SemaphoreSlim` 防止同一任务重入
- 内置简体中文和英文
- 支持加载完整或部分 JSON 语言包
- Debug 模式支持 HotAvalonia Hot Reload
- Demo 使用 `AvaloniaUI.DiagnosticsSupport 2.2.3`
- 可在 Windows、macOS 和 Linux 桌面应用中使用

> 调度器只在宿主应用进程运行期间有效。程序退出、电脑关机或休眠期间不会执行任务。

## 设计结构

```text
ScheduleEditor
    编辑、显示、校验和本地化
             ↓
ScheduleEditorViewModel
             ↓
ScheduleManager
    读取、保存、恢复和应用调度
       ↓                 ↓
IScheduleStore       FluentScheduleService
       ↓
JsonScheduleStore / SQLite / 宿主自定义存储
```

控件不会擅自决定文件位置。组件库提供 `JsonScheduleStore`，但保存路径由宿主程序指定。

## 项目结构

```text
src/AvaloniaScheduleEditor
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

demo/AvaloniaScheduleEditor.Demo
  完整调用示例、JSON 恢复、语言切换、外部日语语言包和执行日志
```

## 还原和运行

### 推荐方式

安装 .NET 10 SDK 后，在解决方案根目录执行：

```powershell
dotnet restore .\AvaloniaScheduleEditor.sln
dotnet run --project .\demo\AvaloniaScheduleEditor.Demo
```

也可以使用 Visual Studio 或 Rider 打开：

```text
AvaloniaScheduleEditor.sln
```

### 遇到 NuGet“访问被拒绝”

项目根目录提供了 `NuGet.Config` 和 `restore-local.cmd`。它们会把 NuGet 包保存到解决方案内的：

```text
.nuget\packages
```

关闭 Visual Studio 后，双击：

```text
restore-local.cmd
```

或手动执行：

```powershell
dotnet restore .\AvaloniaScheduleEditor.sln `
  --packages .\.nuget\packages `
  --force `
  --no-cache

dotnet build .\AvaloniaScheduleEditor.sln `
  -c Debug `
  --no-restore
```

如果先出现“NuGet 包还原失败：对路径的访问被拒绝”，随后又出现 `NCrontab` 找不到或组件 DLL 找不到，后两项通常只是还原失败造成的连锁错误。

# 在程序 A 中使用

## 1. 引用组件项目

```xml
<ItemGroup>
  <ProjectReference Include="..\AvaloniaScheduleEditor\AvaloniaScheduleEditor.csproj" />
</ItemGroup>
```

如果之后将组件打包为 NuGet，则改为对应的 `PackageReference`。

## 2. 在界面中放置控件

默认只显示每天、每周和每月：

```xml
<Window
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:controls="using:AvaloniaScheduleEditor.Controls">

  <controls:ScheduleEditor
      DataContext="{Binding ScheduleEditor}" />
</Window>
```

## 3. 控制重复方式是否显示

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

属性默认值：

| 属性 | 默认值 | 对应方式 |
|---|---:|---|
| `ShowEverySeconds` | `False` | 每 N 秒 |
| `ShowEveryMinutes` | `False` | 每 N 分钟 |
| `ShowEveryHours` | `False` | 每 N 小时 |
| `ShowDaily` | `True` | 每天 |
| `ShowWeekly` | `True` | 每周 |
| `ShowMonthly` | `True` | 每月 |
| `ShowCron` | `False` | 自定义 Cron |

这些属性都是 Avalonia `StyledProperty`，可以绑定到宿主 ViewModel：

```xml
<controls:ScheduleEditor
    DataContext="{Binding ScheduleEditor}"
    ShowCron="{Binding AllowAdvancedCron}"
    ShowEverySeconds="{Binding AllowSecondInterval}" />
```

如果当前选中的模式被隐藏，控件会自动切换到第一个可见模式。宿主必须至少开放一种重复方式。

## 4. 创建存储、调度管理器和编辑器 ViewModel

```csharp
using AvaloniaScheduleEditor.Localization;
using AvaloniaScheduleEditor.Models;
using AvaloniaScheduleEditor.Services;
using AvaloniaScheduleEditor.ViewModels;

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

        // 组件提供 JSON 存储实现，宿主决定文件位置。
        _store = new JsonScheduleStore(settingsPath);

        // 到点后调用 ExecuteScheduledTaskAsync。
        _scheduleManager = new ScheduleManager(
            _store,
            ExecuteScheduledTaskAsync);

        // 可传入 zh-CN、en-US，或已注册的外部语言。
        _localization = new ScheduleLocalizationService("zh-CN");

        ScheduleEditor = new ScheduleEditorViewModel(_localization)
        {
            // 用户点击保存后：验证、写 JSON、停止旧规则并应用新规则。
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
        // 替换成程序 A 的真实业务：采集、导出、上传、清理等。
        await Task.Delay(1000, cancellationToken);
    }

    public void Dispose()
    {
        _scheduleManager.Dispose();
    }
}
```

程序 A 不需要直接创建 FluentScheduler 的 `Schedule`；`ScheduleManager` 和 `FluentScheduleService` 已封装创建、停止、重新应用和防重入逻辑。

## 5. 启动时读取 JSON 并恢复调度

```csharp
public async Task InitializeAsync()
{
    var options = await _scheduleManager.InitializeAsync(
        new ScheduleOptions
        {
            // 第一次运行时使用的默认设置。
            IsEnabled = false,
            RepeatType = ScheduleRepeatType.Daily,
            ExecutionTime = new TimeSpan(9, 30, 0)
        });

    // 将实际读取或默认生成的配置载入控件。
    ScheduleEditor.Load(options);
}
```

`InitializeAsync()` 会：

1. 从 `IScheduleStore` 读取配置；
2. 找不到配置时使用传入的默认值；
3. 校验并规范化配置；
4. 配置为启用时恢复 FluentScheduler；
5. 返回实际生效的配置。

窗口生命周期示例：

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

# 支持的配置

## 每 10 秒

```csharp
var options = new ScheduleOptions
{
    IsEnabled = true,
    RepeatType = ScheduleRepeatType.EverySeconds,
    Interval = 10
};
```

## 每 15 分钟

```csharp
var options = new ScheduleOptions
{
    IsEnabled = true,
    RepeatType = ScheduleRepeatType.EveryMinutes,
    Interval = 15
};
```

## 每 2 小时

```csharp
var options = new ScheduleOptions
{
    IsEnabled = true,
    RepeatType = ScheduleRepeatType.EveryHours,
    Interval = 2
};
```

## 每天 09:30

```csharp
var options = new ScheduleOptions
{
    IsEnabled = true,
    RepeatType = ScheduleRepeatType.Daily,
    ExecutionTime = new TimeSpan(9, 30, 0)
};
```

## 每周一、三、五 09:30

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

## 每月 15 日 09:30

```csharp
var options = new ScheduleOptions
{
    IsEnabled = true,
    RepeatType = ScheduleRepeatType.Monthly,
    DayOfMonth = 15,
    ExecutionTime = new TimeSpan(9, 30, 0)
};
```

如果某个月不存在指定日期，例如 2 月 30 日，该月会跳过。

## 自定义 Cron

```csharp
var options = new ScheduleOptions
{
    IsEnabled = true,
    RepeatType = ScheduleRepeatType.Cron,
    CronExpression = "*/10 * * * * *"
};
```

支持：

```text
5 段：分 时 日 月 星期
6 段：秒 分 时 日 月 星期
```

示例：

```text
0 9 * * *        每天 09:00
0 30 9 * * *     每天 09:30:00
*/10 * * * * *   每 10 秒
```

Cron 使用 NCrontab 进行格式校验和下次执行计算。普通用户不需要启用 Cron 模式。

## 直接保存并应用配置

```csharp
await _scheduleManager.SaveAndApplyAsync(options);
```

该方法会先完成持久化，保存成功后再重新应用调度。

# JSON 持久化

例如每周一、三、五 09:30：

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

`JsonScheduleStore`：

- 使用字符串保存枚举；
- 自动创建父目录；
- 先写临时文件，再原子替换目标文件；
- 支持传入自定义 `JsonSerializerOptions`；
- 文件路径由宿主程序决定。

程序关闭后 JSON 仍然存在。下次启动必须调用 `ScheduleManager.InitializeAsync()` 才会恢复调度。

# 使用 SQLite 或其他存储

实现 `IScheduleStore`：

```csharp
public sealed class SqliteScheduleStore : IScheduleStore
{
    public Task<ScheduleOptions?> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        // 从 SQLite 读取。
        throw new NotImplementedException();
    }

    public Task SaveAsync(
        ScheduleOptions options,
        CancellationToken cancellationToken = default)
    {
        // 写入 SQLite。
        throw new NotImplementedException();
    }
}
```

然后替换存储对象：

```csharp
IScheduleStore store = new SqliteScheduleStore();

_scheduleManager = new ScheduleManager(
    store,
    ExecuteScheduledTaskAsync);
```

控件和调度代码无需改变。

# 多语言

## 内置语言

组件内置：

- `zh-CN`：简体中文
- `en-US`：English

宿主程序必须创建并长期复用同一个 `ScheduleLocalizationService` 实例，再把它传给 `ScheduleEditorViewModel`：

```csharp
private readonly ScheduleLocalizationService _localization;

public MainViewModel()
{
    _localization = new ScheduleLocalizationService("zh-CN");

    ScheduleEditor = new ScheduleEditorViewModel(_localization)
    {
        SaveHandler = options =>
            _scheduleManager.SaveAndApplyAsync(options)
    };
}

public ScheduleEditorViewModel ScheduleEditor { get; }
```

不要分别为语言选择器和编辑器创建两个本地化服务，否则注册到其中一个实例的外部语言包不会出现在另一个实例中。

运行时切换：

```csharp
_localization.SetCulture("en-US");
_localization.SetCulture("zh-CN");
```

编辑器会监听 `LanguageChanged`，切换后立即刷新组件文字。

## 程序 A 传入外部 JSON 语言包

外部语言包不是由控件自动扫描的。宿主程序需要完成以下三步：

1. 将 JSON 文件放进程序 A；
2. 设置构建时复制到输出目录；
3. 程序启动时调用本地化服务加载。

### 第一步：把语言文件加入程序 A

例如：

```text
ProgramA/
  Languages/
    ja-JP.json
```

完整语言包格式：

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

字段说明：

| 字段 | 是否必需 | 说明 |
|---|---:|---|
| `culture` | 是 | 语言标识，例如 `ja-JP`、`de-DE` |
| `displayName` | 否 | 语言选择器中显示的名称；省略时显示 culture |
| `fallbackCulture` | 否 | 缺少文本时使用的回退语言；新语言默认回退到 `en-US` |
| `strings` | 否 | 语言键和值；允许只提供部分键 |

JSON 属性名不区分大小写，并允许尾随逗号与注释。建议保存为 UTF-8。

### 第二步：复制语言文件到输出目录

在程序 A 的 `.csproj` 中加入：

```xml
<ItemGroup>
  <None Update="Languages\*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

发布后应能看到：

```text
程序输出目录/
  ProgramA.exe
  Languages/
    ja-JP.json
```

### 第三步：启动时加载并传给组件

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

这里必须继续使用创建 `ScheduleEditorViewModel` 时传入的 `_localization`，这就是“外部程序把语言包传给组件”的连接点。

推荐使用 `AppContext.BaseDirectory` 组合路径，不要依赖当前工作目录，因为从快捷方式、服务或不同 IDE 启动时，当前工作目录可能变化。

`setCurrent` 的含义：

- `false`：只注册语言，不切换当前界面；
- `true`：注册后立即切换到该语言。

也可以通过编辑器 ViewModel 加载，它会转发给同一个本地化服务：

```csharp
await ScheduleEditor.AddOrUpdateLanguagePackFromFileAsync(
    languageFile,
    setCurrent: true);
```

## 直接传入 JSON 字符串

适合语言包来自数据库、网络接口、嵌入资源或宿主自己的配置系统：

```csharp
var languageJson = await File.ReadAllTextAsync(languageFile);

_localization.AddOrUpdateLanguagePackFromJson(
    languageJson,
    setCurrent: true);
```

如果宿主拿到的是 `Stream`，先读取成字符串：

```csharp
using var reader = new StreamReader(stream);
var languageJson = await reader.ReadToEndAsync(cancellationToken);

_localization.AddOrUpdateLanguagePackFromJson(
    languageJson,
    setCurrent: true);
```

## 直接传入对象

不使用 JSON 文件时，可以直接构造 `ScheduleLanguagePack`：

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

这种方式适合程序 A 已经有自己的资源系统，运行时把资源内容转换为字典后传给组件。

## 只覆盖内置语言的部分文本

局部覆盖 JSON 是一个**扁平的键值对象**，与完整语言包格式不同：

```json
{
  "Title": "自动任务计划",
  "Save": "应用设置",
  "Cancel": "放弃修改"
}
```

加载到内置中文：

```csharp
var overrideJson = await File.ReadAllTextAsync(
    Path.Combine(
        AppContext.BaseDirectory,
        "Languages",
        "zh-CN.override.json"));

_localization.AddOrUpdateLanguageOverridesFromJson(
    culture: "zh-CN",
    json: overrideJson,
    displayName: "客户定制中文",
    fallbackCulture: "en-US",
    setCurrent: true);
```

同一个 culture 被再次注册时，`strings` 会合并；新提供的键覆盖旧值，未提供的键保持原值。因此客户不需要复制完整的内置语言包。

## 启动时加载多个客户语言包

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

    // 所有语言注册完成后再切换。
    _localization.SetCulture("ja-JP");
}
```

如果目录同时包含局部覆盖文件，建议使用不同后缀，例如 `*.override.json`，并分别调用 `AddOrUpdateLanguageOverridesFromJson()`，因为两种 JSON 结构不同。

## 语言选择器

```csharp
public IReadOnlyList<ScheduleLanguageInfo> Languages =>
    _localization.AvailableLanguages;

public void ChangeLanguage(ScheduleLanguageInfo language)
{
    _localization.SetCulture(language.Culture);
}
```

Avalonia 示例：

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

外部语言注册后会触发 `LanguagesChanged`。如果宿主使用 `ObservableCollection` 展示语言列表，应在该事件中刷新集合；从后台线程加载语言包时，更新 UI 集合需要使用 `Dispatcher.UIThread.Post`。

## 保存客户选择的语言

语言包注册和当前语言都只保存在内存中：

- 不会写入 `schedule.json`；
- 程序关闭后不会自动记住外部语言；
- 下次启动必须重新加载外部语言包；
- 需要宿主单独保存用户选择的 culture，例如 `ja-JP`。

示例：

```csharp
// 保存用户选择。
await File.WriteAllTextAsync(
    cultureSettingsPath,
    _localization.CurrentCulture);

// 下次启动：先注册外部语言，再恢复选择。
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

调用 `SetCulture()` 前必须先注册对应语言，否则会抛出 `KeyNotFoundException`。

## 回退规则与版本兼容

缺失文本按以下顺序查找：

```text
当前语言 → fallbackCulture → en-US → 文本键名
```

因此组件后续增加新语言键时，旧客户语言包不会立即导致界面报错；未翻译的新键会回退到英文。

组件只本地化 `ScheduleEditor` 内部文字。程序 A 自己的窗口标题、日志、按钮和状态文本仍由程序 A 自己本地化。

## 格式占位符注意事项

以下键包含格式占位符，客户翻译时必须保留对应的 `{0}`、`{1}`：

| 语言键 | 必需占位符 |
|---|---|
| `SummaryEverySeconds` | `{0}` |
| `SummaryEveryMinutes` | `{0}` |
| `SummaryEveryHours` | `{0}` |
| `SummaryDaily` | `{0}` |
| `SummaryWeekly` | `{0}`、`{1}` |
| `SummaryMonthly` | `{0}`、`{1}` |
| `SummaryCron` | `{0}` |
| `ErrorInvalidCron` | `{0}` |
| `ErrorSaveFailed` | `{0}` |

`DateTimeFormat` 使用 .NET 日期时间格式字符串，例如：

```json
{
  "DateTimeFormat": "yyyy-MM-dd ddd HH:mm:ss"
}
```

如果客户提供的格式占位符无效，组件不会崩溃，但会退回显示原始模板文字。

## 可用语言键

完整键列表位于：

```text
src/AvaloniaScheduleEditor/Localization/ScheduleTextKeys.cs
```

可直接复制的完整外部语言包示例位于：

```text
demo/AvaloniaScheduleEditor.Demo/Languages/ja-JP.json
```

语言键分为：

- 标题、开关和重复方式；
- 时间、间隔、星期、月份及 Cron 字段；
- 摘要和下次执行提示；
- 保存反馈和校验错误；
- 日期格式与列表分隔符；
- 星期短名称和完整名称。

## 常见问题

### 加载时报找不到文件

检查语言 JSON 是否配置了 `CopyToOutputDirectory`，并打印实际路径：

```csharp
Console.WriteLine(languageFile);
```

### `SetCulture("ja-JP")` 提示未注册

必须先调用 `AddOrUpdateLanguagePack...` 注册，再调用 `SetCulture()`；或者加载时设置 `setCurrent: true`。

### 注册成功但界面没有变化

确认 `ScheduleEditorViewModel` 和外部代码使用的是同一个 `ScheduleLocalizationService` 实例。

### 只有部分文字被翻译

这是允许的行为。缺少的键会按回退链显示。如果需要完全翻译，请以 `ScheduleTextKeys.cs` 或 Demo 的日语文件为模板补齐。

# 执行事件

```csharp
_scheduleManager.ExecutionStarted += (_, e) =>
{
    Console.WriteLine($"开始：{e.StartedAt}");
};

_scheduleManager.ExecutionCompleted += (_, e) =>
{
    Console.WriteLine($"完成，耗时：{e.Duration}");
};

_scheduleManager.ExecutionFailed += (_, e) =>
{
    Console.WriteLine($"失败：{e.Exception?.Message}");
};

_scheduleManager.ExecutionSkipped += (_, _) =>
{
    Console.WriteLine("上一次任务尚未结束，本次触发已跳过。");
};
```

`FluentScheduleService` 使用非阻塞 `SemaphoreSlim`。如果上一次任务仍在运行，新触发不会并行执行，而是触发 `ExecutionSkipped`。

# Hot Reload

组件项目和 Demo 项目的 Debug 配置均包含：

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

使用方法：

1. 使用 Debug 配置启动 Demo；
2. 修改并保存 `.axaml` 文件；
3. HotAvalonia 自动重新载入界面；
4. 未自动刷新时按 `Alt + F5` 强制刷新。

这些依赖只在 Debug 下引用，不会进入普通 Release 依赖链。

# Avalonia Developer Tools

Demo 在 Debug 下引用：

```xml
<PackageReference Include="AvaloniaUI.DiagnosticsSupport"
                  Version="2.2.3" />
```

`App.Initialize()`：

```csharp
#if DEBUG
this.AttachDeveloperTools();
#endif
```

旧的 `Avalonia.Diagnostics` 已移除。

# 调度实现说明

- 每 N 秒、分钟、小时直接使用 FluentScheduler 的间隔规则；
- 每天模式使用固定时间规则；
- 每周和每月模式每天到点唤醒一次，再由组件判断星期或日期是否匹配；
- 自定义 Cron 直接交给 FluentScheduler；
- `NextRun` 对每周和每月使用 `ScheduleCalculator` 计算真实业务时间；
- 所有配置在应用前都会经过 `ScheduleOptionsValidator`；
- `ScheduleOptions.Normalize()` 会去除秒、整理星期顺序并清理 Cron 首尾空格。

# 注意事项

- 组件只在应用进程运行期间执行；
- 应用退出后不会继续执行；
- 电脑关机期间不会执行；
- 系统休眠期间错过的任务不会自动补执行；
- 应用再次启动后应调用 `ScheduleManager.InitializeAsync()`；
- 间隔模式从应用配置生效时开始计时，而不是对齐系统整点；
- 一个 `ScheduleManager` 管理一个任务；多个独立任务建议分别创建各自的存储路径和 `ScheduleManager`；
- 业务任务应正确响应 `CancellationToken`；
- 不要在调度任务中直接阻塞 Avalonia UI 线程；需要更新界面时使用 `Dispatcher.UIThread.Post`。
- 外部语言包和当前 culture 不会由 `JsonScheduleStore` 保存；宿主应在每次启动时重新注册语言包，并按需单独保存用户语言选择。
- `ScheduleEditorViewModel` 实现了 `IDisposable`，窗口或宿主 ViewModel 释放时应调用 `Dispose()` 以取消语言事件订阅。
