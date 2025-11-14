# Known Issues

## 編譯錯誤

目前專案有一些編譯錯誤需要修復。以下是主要問題和解決方案：

### 1. 建構子中的欄位指派問題

**問題：** 多個類別的建構子中，欄位指派使用了相同的變數名稱（例如：`repository = repository`）。

**影響檔案：**
- `Data/Repository.cs`
- `Commands/UserCommands.cs`
- `Commands/ChatCommands.cs`
- `Commands/AdminCommands.cs`
- `Services/DailyCleanupService.cs`
- `Services/CommandHandlerService.cs`
- `Services/LLMService.cs`
- `Services/DiscordBotService.cs`

**解決方法：** 
將建構子參數名稱改為有下劃線前綴的欄位名稱：

```csharp
// 錯誤的
public Repository(BotDbContext context, ILogger logger)
{
    context = context;  // 這會指派給自己
    logger = logger;
}

// 正確的
public Repository(BotDbContext context, ILogger logger)
{
    _context = context;
    _logger = logger;
}
```

### 2. Serilog Configuration 問題

**問題：** `Program.cs` 中 Serilog 的 `ReadFrom.Configuration()` 方法調用有誤。

**位置：** `Program.cs` 第 20 行

**解決方法：**
需要添加 `Serilog.Settings.Configuration` 套件：

```bash
dotnet add package Serilog.Settings.Configuration
```

然後修改代碼：

```csharp
using Serilog.Settings.Configuration;

// 在 Main 方法中
var configuration = BuildConfiguration();
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();
```

### 3. TokenControlService 中的欄位使用問題

**問題：** 在使用 replace_all 替換 `repository`、`config` 和 `logger` 時，部分引用沒有正確替換為 `_repository`、`_config` 和 `_logger`。

**位置：** `Services/TokenControlService.cs`

**解決方法：** 確保所有欄位引用都使用正確的下劃線前綴。

### 4. LLMService 和 DiscordBotService 中的類型錯誤

**問題：** 建構子中試圖將 `Config` 物件指派給 `IOptions<Config>` 型別的欄位。

**位置：** 
- `Services/LLMService.cs` 第 27 行
- `Services/DiscordBotService.cs` 第 25 行

**解決方法：**
欄位應該儲存 `.Value` 屬性：

```csharp
// LLMService.cs
private readonly LLMConfig _config;  // 不是 IOptions<LLMConfig>

public LLMService(IOptions<LLMConfig> config, ...)
{
    _config = config.Value;  // 取得實際的 config 物件
    ...
}
```

## 快速修復指南

1. 為所有類別建構子中的欄位指派添加正確的欄位名稱
2. 安裝 `Serilog.Settings.Configuration` 套件
3. 修正 TokenControlService 中的欄位引用
4. 修正 LLMService 和 DiscordBotService 的類型

## 建議的修復順序

1. 先修復 `Program.cs` 的 Serilog 設定問題
2. 修復所有服務類別的建構子欄位指派
3. 修復命令類別的建構子欄位指派
4. 重新編譯並檢查錯誤

完成這些修復後，專案應該可以正常編譯和執行。

