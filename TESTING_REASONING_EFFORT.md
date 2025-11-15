# Reasoning Effort 功能測試指南

## 功能概述
已成功實作 `reasoning_effort` 參數支援，允許在呼叫 LLM API 時指定推理深度等級。

## 實作內容

### 1. LLMService 修改
- ✅ 在 `GetChatCompletionStreamingAsync` 方法中加入 `reasoningEffort` 參數
- ✅ 使用 `OpenAIPromptExecutionSettings.ExtensionData` 傳遞 `reasoning_effort` 參數到 API
- ✅ 從 API 回應的 metadata 中提取 `reasoning` 內容
- ✅ 修改返回值為 `(string content, string? reasoning, int? promptTokens, int? completionTokens)`

### 2. ChatCommands 修改
- ✅ 在 `/chat` 命令中加入 `reasoning-effort` 可選參數
- ✅ 提供三個選項：low、medium、high（預設：medium）
- ✅ 實時串流顯示推理過程在獨立的 follow-up 訊息中
- ✅ 推理訊息使用紫色 embed，標題為 "🧠 推理過程"
- ✅ 推理過程完成後顯示最終狀態和使用的 reasoning_effort 等級

## 測試步驟

### 前置條件
1. 確保 Discord Bot 正在運行
2. 確保 LLM API 端點支援 `reasoning_effort` 參數
3. 確保您在 Discord 伺服器中有使用 bot 的權限

### 測試案例 1：使用預設 reasoning_effort (medium)
```
/chat message: "解釋什麼是量子力學"
```
**預期結果：**
- Bot 回應分為兩個 embed 訊息
- 第一個訊息（紫色）顯示推理過程，標題為 "🧠 推理過程"
- 第二個訊息（藍色）顯示最終回應內容
- 推理訊息的 footer 顯示 "推理完成 | 使用 reasoning_effort: medium"

### 測試案例 2：使用 low reasoning_effort
```
/chat message: "Hi" reasoning-effort: low
```
**預期結果：**
- 快速回應
- 如果 API 返回 reasoning 內容，會顯示推理過程（通常較簡短）
- footer 顯示 "推理完成 | 使用 reasoning_effort: low"

### 測試案例 3：使用 high reasoning_effort
```
/chat message: "請詳細分析並比較三種排序演算法的時間複雜度和空間複雜度" reasoning-effort: high
```
**預期結果：**
- 回應較慢（因為推理更深入）
- 推理過程內容更詳細、更完整
- footer 顯示 "推理完成 | 使用 reasoning_effort: high"

### 測試案例 4：串流更新
```
/chat message: "寫一篇關於人工智慧的短文" reasoning-effort: high
```
**預期結果：**
- 推理訊息會即時更新（每秒最多一次）
- 在串流過程中會看到 "正在推理..." 的 footer
- 主要回應訊息也會即時更新顯示生成的內容
- 完成後推理訊息 footer 變為 "推理完成 | 使用 reasoning_effort: high"

### 測試案例 5：沒有 reasoning 內容的情況
如果 API 不返回 reasoning 內容（某些模型或端點可能不支援）：
```
/chat message: "測試" reasoning-effort: medium
```
**預期結果：**
- 只顯示主要回應訊息（藍色）
- 不會顯示推理過程訊息

## 驗證要點

1. **API 請求正確性**
   - 檢查日誌確認 `reasoning_effort` 參數已加入到請求中
   - 日誌應包含："Added reasoning_effort parameter: {選擇的等級}"

2. **Reasoning 內容提取**
   - 檢查日誌確認是否成功提取 reasoning
   - 日誌應包含："Received reasoning content: {字數} characters"

3. **UI 顯示正確性**
   - 推理訊息使用紫色 embed
   - 主要回應使用藍色 embed
   - 推理訊息在主要回應之前顯示

4. **長度限制處理**
   - 如果 reasoning 內容超過 4000 字元，應正確截斷並加上 "..."
   - 如果主要回應超過 1900 字元，應分割成多個訊息

5. **錯誤處理**
   - 如果推理訊息更新失敗，不應影響主要回應的顯示
   - 錯誤應記錄在日誌中但不中斷服務

## API Response 格式參考

根據提供的範例，API 應返回類似以下格式：
```json
{
    "id": "chatcmpl-xxx",
    "object": "chat.completion",
    "model": "openai/gpt-oss-20b",
    "choices": [
        {
            "index": 0,
            "message": {
                "role": "assistant",
                "content": "Hello! 👋 How can I assist you today?",
                "reasoning": "User says \"Hi\". We should respond politely. Probably ask how can help.",
                "tool_calls": []
            },
            "finish_reason": "stop"
        }
    ],
    "usage": {
        "prompt_tokens": 81,
        "completion_tokens": 36,
        "total_tokens": 117
    }
}
```

## 已知限制

1. Reasoning 內容的提取依賴於 API 在 metadata 中返回 `reasoning` 或 `Reasoning` 欄位
2. 不同的 LLM 端點可能對 `reasoning_effort` 的支援程度不同
3. Discord embed 描述限制為 4096 字元，超長的 reasoning 會被截斷

## 除錯建議

如果功能不如預期工作：

1. **檢查日誌**
   - 尋找 "Added reasoning_effort parameter" 確認參數已發送
   - 尋找 "Received reasoning content" 確認收到 reasoning 數據

2. **檢查 API 端點**
   - 確認您的 LLM API 端點支援 `reasoning_effort` 參數
   - 嘗試直接使用 API 測試工具驗證

3. **檢查 Metadata 提取**
   - 如果沒有收到 reasoning，可能是 metadata 中的欄位名稱不同
   - 可能需要調整 LLMService 中的提取邏輯

## 結論

✅ Reasoning effort 功能已完整實作
✅ 編譯成功，無錯誤
✅ 支援三個等級：low、medium、high
✅ 實時串流顯示推理過程
✅ 完整的錯誤處理和日誌記錄

