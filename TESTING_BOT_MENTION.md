# 測試 @Bot 提及功能

本文檔說明如何測試新增的 @Bot 提及功能。

## 功能概述

現在 Bot 支援兩種方式來觸發聊天功能：
1. 使用 `/chat` 斜線命令（現有功能）
2. 在訊息中 @提及 Bot（新功能）

## 測試場景

### 場景 1：基本提及測試

**輸入：**
```
@Bot 你好，請告訴我今天的日期
```

**預期行為：**
- Bot 應該回覆訊息
- 使用預設的 reasoning-effort: medium
- 訊息內容應該是："你好，請告訴我今天的日期"

### 場景 2：帶有 reasoning-effort 參數的提及

**輸入：**
```
@Bot [high] 請解釋量子力學的基本原理
```

**預期行為：**
- Bot 應該回覆訊息
- 使用 reasoning-effort: high
- 訊息內容應該是："請解釋量子力學的基本原理"
- 如果 LLM 支援 reasoning 功能，應該會看到 "🧠 推理過程" 的訊息

### 場景 3：提及在訊息中間

**輸入：**
```
請幫我 @Bot 解釋這個概念
```

**預期行為：**
- Bot 應該回覆訊息
- 使用預設的 reasoning-effort: medium
- 訊息內容應該是："請幫我 解釋這個概念"（@Bot 被移除）

### 場景 4：使用不同的 reasoning-effort 參數

**測試 [low]：**
```
@Bot [low] 簡單問題測試
```

**測試 [medium]：**
```
@Bot [medium] 中等問題測試
```

**測試 [high]：**
```
@Bot [high] 複雜問題測試
```

**預期行為：**
- 每個測試都應該使用對應的 reasoning-effort 參數
- 參數應該被正確解析並從訊息中移除

### 場景 5：空訊息測試

**輸入：**
```
@Bot
```

**預期行為：**
- Bot 應該回覆："請提供訊息內容。"

### 場景 6：流式輸出測試

**輸入：**
```
@Bot 請寫一個關於春天的短詩
```

**預期行為：**
- Bot 應該使用流式輸出
- 訊息應該每 2 秒更新一次
- 最終訊息應該包含完整回應
- 應該顯示使用的 token 數量

## 測試檢查清單

- [ ] Bot 正確識別 @提及
- [ ] Bot 忽略自己和其他 Bot 的訊息
- [ ] 正確解析 reasoning-effort 參數（[low], [medium], [high]）
- [ ] 正確移除 @Bot 提及和參數標記
- [ ] 使用回覆功能回應原始訊息
- [ ] 流式輸出正常工作
- [ ] Token 使用正確記錄和顯示
- [ ] 聊天歷史正確保存
- [ ] 錯誤處理正常工作
- [ ] Guild 設定正確應用（如果在伺服器中）
- [ ] 私訊（DM）也能正常工作

## 注意事項

1. **權限：** 確保 Bot 有以下權限：
   - Read Messages/View Channels
   - Send Messages
   - Send Messages in Threads
   - Embed Links
   - Read Message History
   - Mention Everyone (用於讀取提及)

2. **Intent 設定：** 在 Discord Developer Portal 中確保以下 Intent 已啟用：
   - MESSAGE CONTENT INTENT
   - GUILD MESSAGES
   - DIRECT MESSAGES

3. **Token 額度：** 確保測試用戶有足夠的 token 額度

4. **LLM 服務：** 確保 LLM 服務（如 LM Studio）正在運行並可訪問

## 與 /chat 命令的比較

| 特性 | /chat 命令 | @Bot 提及 |
|------|-----------|----------|
| 觸發方式 | 使用 `/chat` | @提及 Bot |
| reasoning-effort | 命令參數選項 | 訊息中的 `[low\|medium\|high]` |
| 回覆方式 | Interaction 回覆 | 訊息回覆 |
| 流式輸出 | ✓ | ✓ |
| Token 控制 | ✓ | ✓ |
| 聊天歷史 | ✓ | ✓ |
| Guild 設定 | ✓ | ✓ |

## 故障排除

如果 Bot 沒有回應：

1. 檢查 Bot 是否在線
2. 檢查 Bot 日誌（應該看到 "Bot mentioned by..." 的訊息）
3. 確認 MESSAGE CONTENT INTENT 已啟用
4. 檢查 Bot 是否有訊息發送權限
5. 確認 LLM 服務正在運行

## 測試結果

測試完成後，請確認：

- ✅ 代碼編譯無錯誤
- ✅ 所有測試場景按預期運行
- ✅ 錯誤處理正確
- ✅ 性能可接受

