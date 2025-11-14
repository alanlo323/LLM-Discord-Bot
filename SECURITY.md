# 安全設定指南

## 敏感信息管理

本專案使用 .NET User Secrets 來管理敏感配置信息（如 Discord Bot Token）。

### 初次設定步驟

1. **複製範例配置文件**
   ```bash
   cd LLMDiscordBot
   cp appsettings.example.json appsettings.json
   ```

2. **設定 Discord Bot Token**
   
   **方法 A：使用 User Secrets（推薦）**
   ```bash
   # 如果尚未初始化
   dotnet user-secrets init
   
   # 設定 Discord Token
   dotnet user-secrets set "Discord:Token" "YOUR_DISCORD_BOT_TOKEN"
   ```

   **方法 B：直接編輯 appsettings.json**
   ```bash
   # 編輯 appsettings.json，將 YOUR_DISCORD_BOT_TOKEN_HERE 替換為實際的 Token
   nano appsettings.json
   ```

3. **驗證設定**
   ```bash
   # 列出所有 User Secrets
   dotnet user-secrets list
   ```

### User Secrets 儲存位置

User Secrets 儲存在以下位置（不會被 Git 追蹤）：

- **Windows**: `%APPDATA%\Microsoft\UserSecrets\<user_secrets_id>\secrets.json`
- **Linux/macOS**: `~/.microsoft/usersecrets/<user_secrets_id>/secrets.json`

本專案的 User Secrets ID: `12792c0e-64ca-4b32-ab73-78ddfaf67e77`

### 配置優先順序

.NET 配置系統會依以下順序覆蓋配置值：
1. `appsettings.json`（基礎配置）
2. User Secrets（開發環境的敏感信息）
3. 環境變數（生產環境）
4. 命令行參數

### 生產環境部署

在生產環境中，**不要使用 User Secrets**。建議使用：

1. **環境變數**
   ```bash
   export Discord__Token="YOUR_DISCORD_BOT_TOKEN"
   export LLM__ApiEndpoint="https://your-api-endpoint.com"
   ```

2. **Docker Secrets**（如果使用 Docker）
   ```yaml
   secrets:
     discord_token:
       external: true
   ```

3. **雲端密鑰管理服務**
   - Azure Key Vault
   - AWS Secrets Manager
   - Google Cloud Secret Manager

### 重新生成 Bot Token

如果您的 Token 被意外洩露：

1. 前往 [Discord Developer Portal](https://discord.com/developers/applications)
2. 選擇您的應用程式
3. 進入 "Bot" 頁面
4. 點擊 "Regenerate" 按鈕
5. 使用新的 Token 更新您的設定

### Git 提交前檢查清單

在提交程式碼前，請確認：

- [ ] `appsettings.json` 已加入 `.gitignore`
- [ ] 沒有在程式碼中硬編碼任何敏感信息
- [ ] 已建立 `appsettings.example.json` 作為範本
- [ ] README.md 中包含設定說明

### 查看當前配置

```bash
# 列出所有 User Secrets
cd LLMDiscordBot
dotnet user-secrets list

# 移除特定設定
dotnet user-secrets remove "Discord:Token"

# 清除所有 User Secrets
dotnet user-secrets clear
```

## 安全最佳實踐

1. **永遠不要**將 Token 或密碼提交到版本控制系統
2. **定期輪換**重要的 API 金鑰和 Token
3. **最小權限原則**：只授予 Bot 所需的最少權限
4. **監控使用情況**：定期檢查 Bot 的活動日誌
5. **使用環境隔離**：開發、測試和生產環境使用不同的 Token

## 報告安全問題

如果您發現任何安全漏洞，請不要公開發布。請通過私密方式聯繫專案維護者。

