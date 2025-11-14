# LLM Discord Bot

一個功能完整的 Discord Bot，使用 Discord.Net、Semantic Kernel 和 SQLite，支援與 OpenAI-compatible API（如 LM Studio）進行聊天互動。

## 功能特點

### 核心功能
- 🤖 **LLM 聊天** - 通過 Discord 斜線命令與 LLM 對話
- 💬 **聊天記錄管理** - 自動維護對話上下文
- 📊 **Token 使用追蹤** - 精確記錄每位用戶的 Token 使用量
- 🎯 **額度控制** - 每日 Token 使用限制，防止濫用
- ⚙️ **動態設定** - 通過命令即時調整 Bot 參數

### 管理功能
- 👥 **用戶管理** - 查看用戶統計、設定額度、封鎖/解封用戶
- 🔧 **Bot 設定** - 動態調整模型、溫度、最大 Token 數、系統提示等
- 📈 **統計查詢** - 查看個人和全域使用統計
- 🗑️ **自動清理** - 定期清理舊資料

### 技術特點
- .NET 8 主控台應用程式
- Discord.Net 3.18.0 - Discord API 整合
- Semantic Kernel - LLM 整合框架
- Entity Framework Core + SQLite - 資料持久化
- Serilog - 結構化日誌記錄

## 系統需求

- .NET 8.0 SDK 或更新版本
- Discord Bot Token
- OpenAI-compatible API 端點（如 LM Studio）

## 快速開始

### 1. 設定 Discord Bot

1. 前往 [Discord Developer Portal](https://discord.com/developers/applications)
2. 建立新應用程式
3. 在 "Bot" 頁面建立 Bot 並複製 Token
4. 在 "OAuth2 > URL Generator" 中：
   - 選擇 `bot` 和 `applications.commands` scope
   - 選擇必要的 Bot 權限（至少需要 "Send Messages", "Use Slash Commands"）
   - 使用生成的 URL 邀請 Bot 到您的伺服器

### 2. 設定應用程式

1. 克隆專案：
```bash
git clone <repository-url>
cd "LLM Discord Bot"
```

2. 編輯 `LLMDiscordBot/appsettings.json`：
```json
{
  "Discord": {
    "Token": "YOUR_DISCORD_BOT_TOKEN_HERE"
  },
  "LLM": {
    "ApiEndpoint": "https://lmstudio.alanlo.org",
    "Model": "default",
    "Temperature": 0.7,
    "MaxTokens": 2000,
    "SystemPrompt": "You are a helpful AI assistant."
  },
  "TokenLimits": {
    "DefaultDailyLimit": 100000,
    "EnableLimits": true
  }
}
```

### 3. 建置和執行

```bash
# 還原套件
dotnet restore

# 建置專案
dotnet build

# 執行 Bot
cd LLMDiscordBot
dotnet run
```

或使用 Visual Studio 2022 開啟 `LLMDiscordBot.sln` 並直接執行。

## Discord 命令

### 聊天命令

#### `/chat <message>`
與 LLM 進行對話。

**參數：**
- `message` - 您想要說的話

**範例：**
```
/chat 你好，請介紹一下自己
```

#### `/clearchat`
清除您在當前頻道的聊天記錄。

### 用戶命令

#### `/mystats`
查看您的使用統計，包括今日使用量、剩餘額度等。

#### `/myhistory [count]`
查看您最近的聊天記錄。

**參數：**
- `count` (可選) - 要顯示的訊息數量（預設：10，最多：50）

### 管理員命令

所有管理員命令都需要 Discord 伺服器的管理員權限。

#### 用戶管理

##### `/admin user-stats <user>`
查看指定用戶的詳細統計資訊。

##### `/admin set-limit <user> <tokens>`
設定用戶的每日 Token 額度。

**範例：**
```
/admin set-limit @User 50000
```

##### `/admin reset-usage <user>`
重置用戶今日的使用量。

##### `/admin block <user>`
封鎖用戶，阻止其使用 Bot。

##### `/admin unblock <user>`
解封用戶。

#### Bot 設定管理

##### `/admin set-model <model>`
設定 LLM 模型名稱。

##### `/admin set-temperature <temperature>`
設定生成溫度（0.0 - 2.0）。

##### `/admin set-max-tokens <max-tokens>`
設定最大回應 Token 數。

##### `/admin set-system-prompt <prompt>`
設定系統提示詞。

##### `/admin set-global-limit <tokens>`
設定全域預設每日 Token 額度（僅影響新用戶）。

##### `/admin view-settings`
查看當前所有 Bot 設定。

##### `/admin stats`
查看全域使用統計。

## 專案結構

```
LLM Discord Bot/
├── LLMDiscordBot.sln              # Visual Studio 解決方案
└── LLMDiscordBot/                  # 主要專案
    ├── Program.cs                  # 應用程式入口點
    ├── appsettings.json            # 設定檔
    ├── Commands/                   # Discord 斜線命令
    │   ├── ChatCommands.cs         # 聊天相關命令
    │   ├── UserCommands.cs         # 用戶查詢命令
    │   └── AdminCommands.cs        # 管理員命令
    ├── Configuration/              # 設定類別
    │   └── BotConfig.cs            # 設定模型
    ├── Data/                       # 資料層
    │   ├── BotDbContext.cs         # EF Core DbContext
    │   ├── BotDbContextFactory.cs  # 設計時工廠
    │   ├── IRepository.cs          # Repository 介面
    │   └── Repository.cs           # Repository 實作
    ├── Models/                     # 資料模型
    │   ├── User.cs                 # 用戶實體
    │   ├── TokenUsage.cs           # Token 使用記錄
    │   ├── ChatHistory.cs          # 聊天記錄
    │   └── BotSettings.cs          # Bot 設定
    ├── Services/                   # 服務層
    │   ├── DiscordBotService.cs    # Discord Bot 主服務
    │   ├── CommandHandlerService.cs # 命令處理服務
    │   ├── LLMService.cs           # LLM 整合服務
    │   ├── TokenControlService.cs  # Token 控制服務
    │   └── DailyCleanupService.cs  # 每日清理服務
    └── Migrations/                 # EF Core 遷移
```

## 資料庫結構

Bot 使用 SQLite 資料庫，包含以下表格：

### Users
儲存用戶資訊和設定
- UserId (主鍵)
- DailyTokenLimit
- IsBlocked
- CreatedAt
- LastAccessAt

### TokenUsage
記錄每日 Token 使用量
- Id (主鍵)
- UserId (外鍵)
- Date
- TokensUsed
- MessageCount
- CreatedAt

### ChatHistory
儲存聊天對話記錄
- Id (主鍵)
- UserId (外鍵)
- ChannelId
- Role (user/assistant)
- Content
- TokenCount
- Timestamp

### BotSettings
儲存 Bot 執行時設定
- Key (主鍵)
- Value
- UpdatedAt
- UpdatedBy

## 環境變數

您可以使用環境變數來覆蓋 `appsettings.json` 中的設定：

```bash
# Windows (PowerShell)
$env:Discord__Token = "your_token_here"
$env:LLM__ApiEndpoint = "https://your-api-endpoint.com"

# Linux/macOS (Bash)
export Discord__Token="your_token_here"
export LLM__ApiEndpoint="https://your-api-endpoint.com"
```

## 日誌

日誌會同時輸出到：
- 控制台（即時查看）
- 檔案 `logs/bot-YYYYMMDD.log`（保留 30 天）

日誌等級可在 `appsettings.json` 中調整。

## 常見問題

### Bot 無法啟動？

1. 確認 Discord Token 是否正確設定
2. 檢查網路連線
3. 查看日誌檔案中的錯誤訊息

### 斜線命令沒有出現？

斜線命令需要最多 1 小時才會在全域註冊。如需即時測試：
1. 在 Discord Developer Portal 獲取您的伺服器 ID
2. 修改 `CommandHandlerService.cs` 中的 `RegisterCommandsAsync` 方法
3. 使用 `RegisterCommandsToGuildAsync(guildId)` 代替 `RegisterCommandsGloballyAsync()`

### LLM 回應錯誤？

1. 確認 API 端點可存取
2. 檢查模型名稱是否正確
3. 查看日誌中的詳細錯誤訊息

## 授權

[MIT License](LICENSE)

## 貢獻

歡迎提交 Issue 和 Pull Request！

## 支援

如有問題或建議，請開啟 Issue。

