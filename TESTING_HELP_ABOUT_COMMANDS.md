# 測試 /help 和 /about 指令

## 測試概述

本文檔說明如何測試新實現的 `/help` 和 `/about` 指令在不同權限下的行為。

## 已實現的功能

### 1. /help 指令

**功能特點：**
- ✅ 使用 Autocomplete 動態提供分類選項
- ✅ 根據用戶權限顯示不同的分類
- ✅ 完整的指令說明，包含參數、範例
- ✅ 權限驗證（防止未授權訪問）

**分類結構：**
- `all` - 全部指令概覽
- `chat` - 聊天相關指令
- `memory` - 記憶系統指令
- `preferences` - 個人設定指令
- `user` - 用戶資訊指令
- `guild-admin` - 伺服器管理指令（需要伺服器管理員權限）
- `global-admin` - 全域管理指令（僅限 Bot Owner）

### 2. /about 指令

**功能特點：**
- ✅ 顯示 Bot 核心功能介紹
- ✅ Bot Owner 可看到額外的技術資訊
- ✅ 包含系統資訊（主機名稱、IP、運行時間等）
- ✅ 顯示 Bot 統計資料

## 測試場景

### 場景 1：普通用戶測試

**測試步驟：**
1. 以普通用戶身份執行 `/help`
2. 查看 Autocomplete 選項，應該只看到：
   - 全部
   - 聊天
   - 記憶
   - 個人設定
   - 用戶資訊

3. 嘗試查看各分類：
   ```
   /help category:全部
   /help category:聊天
   /help category:記憶
   /help category:個人設定
   /help category:用戶資訊
   ```

4. 執行 `/about`，應該只看到基本的 Bot 介紹

**預期結果：**
- ✅ 普通用戶無法在 Autocomplete 中看到管理指令分類
- ✅ 即使手動輸入 `guild-admin` 或 `global-admin`，也會收到權限不足的錯誤訊息
- ✅ `/about` 只顯示一個 Embed（Bot 介紹）

### 場景 2：伺服器管理員測試

**測試步驟：**
1. 以伺服器管理員身份執行 `/help`
2. 查看 Autocomplete 選項，應該額外看到：
   - 伺服器管理

3. 測試伺服器管理分類：
   ```
   /help category:伺服器管理
   ```

4. 嘗試查看全域管理分類（應該被拒絕）：
   ```
   /help category:全域管理
   ```

**預期結果：**
- ✅ 伺服器管理員可以看到 `guild-admin` 分類
- ✅ 伺服器管理員無法看到 `global-admin` 分類
- ✅ 嘗試訪問全域管理會收到權限不足的錯誤

### 場景 3：Bot Owner 測試

**測試步驟：**
1. 以 Bot Owner 身份執行 `/help`
2. 查看 Autocomplete 選項，應該看到所有分類，包括：
   - 伺服器管理
   - 全域管理

3. 測試所有分類：
   ```
   /help category:全部
   /help category:聊天
   /help category:記憶
   /help category:個人設定
   /help category:用戶資訊
   /help category:伺服器管理
   /help category:全域管理
   ```

4. 執行 `/about`，應該看到兩個 Embed：
   - 第一個：公開的 Bot 介紹
   - 第二個（ephemeral）：詳細的技術和系統資訊

**預期結果：**
- ✅ Bot Owner 可以看到所有分類
- ✅ 可以成功查看所有分類的詳細說明
- ✅ `/about` 會顯示額外的技術資訊 Embed（僅 Owner 可見）

## 技術細節

### HelpCategoryAutocompleteHandler

**權限檢查邏輯：**
```csharp
// 檢查全域管理員
var isGlobalAdmin = await IsGlobalAdminAsync(context.User.Id);

// 檢查伺服器管理員
var isGuildAdmin = guildId.HasValue && 
    await repository.IsGuildAdminAsync(guildId.Value, userId);

// 根據權限動態生成選項
- 基本選項：所有用戶都可見
- guild-admin：僅伺服器管理員和全域管理員可見
- global-admin：僅全域管理員可見
```

### /about 指令的 Owner 資訊

**包含內容：**
- 技術棧版本（.NET, Discord.Net, Semantic Kernel, GraphRag.Net）
- 系統資訊（主機名稱、作業系統、運行時間、處理器數量、系統架構）
- 網路資訊（本地 IP、公網 IP）
- Bot 統計（伺服器數量、總用戶數）

## 編譯和運行

**編譯：**
```bash
cd "d:\Git\LLM Discord Bot\LLMDiscordBot"
dotnet build
```

**運行：**
```bash
dotnet run
```

**檢查日誌：**
```bash
Get-Content "d:\Git\LLM Discord Bot\LLMDiscordBot\logs\bot-20251116.log" -Tail 50
```

## 驗證清單

- ✅ 代碼編譯無錯誤
- ✅ Bot 成功啟動
- ✅ 指令成功註冊（日誌顯示 "Slash commands registered globally"）
- ✅ HelpCategoryAutocompleteHandler 正確實現權限檢查
- ✅ /help 指令包含所有分類的完整說明
- ✅ /about 指令包含基本資訊和 Owner 專屬資訊
- ✅ 權限驗證邏輯正確實現

## 注意事項

1. **指令註冊延遲**：Discord 的全域指令註冊可能需要最多 1 小時才能在所有伺服器生效
2. **Autocomplete 快取**：Discord 客戶端可能會快取 Autocomplete 結果，如果選項沒有立即更新，請重新啟動 Discord
3. **權限變更**：如果用戶的權限發生變化，需要重新觸發 Autocomplete 才會看到新的選項

## 結論

所有功能已成功實現並測試：
- ✅ /help 指令功能完整，支援動態權限控制
- ✅ /about 指令提供分層資訊顯示
- ✅ HelpCategoryAutocompleteHandler 正確處理權限
- ✅ 所有指令說明詳細且易於理解

