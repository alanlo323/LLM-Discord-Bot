# GraphRAG 自動記憶系統實作完成

## 概述

已成功實作 GraphRAG 自動記憶系統，整合 GraphRag.Net 套件，提供智能對話記憶功能。

## 已完成的功能

### 1. 配置系統 ✅
- **檔案**: `appsettings.json`, `appsettings.example.json`
- **配置類別**: `Configuration/BotConfig.cs`
- 新增完整的 GraphRag 配置區段
- 包含 OpenAI、文本分塊、資料庫連接、搜索參數等配置

### 2. 核心服務 ✅

#### GraphMemoryService (`Services/GraphMemoryService.cs`)
- **功能**: 記憶索引管理、儲存、檢索
- **主要方法**:
  - `GetUserMemoryIndex()` - 生成用戶記憶索引名稱
  - `StoreConversationMemoryAsync()` - 儲存對話記憶
  - `SearchRelevantMemoriesAsync()` - 搜索相關記憶
  - `ShouldRetrieveMemoryAsync()` - 智能判斷是否檢索記憶
  - `DeleteMemoryIndexAsync()` - 刪除記憶索引
  - `GetMemoryStatsAsync()` - 獲取記憶統計

#### MemoryAnalyzerService (`Services/MemoryAnalyzerService.cs`)
- **功能**: 使用 LLM 分析對話並提取重要資訊
- **主要方法**:
  - `AnalyzeConversationForMemoryAsync()` - 分析對話並提取記憶內容
  - `IsMemoryWorthyAsync()` - 判斷對話是否值得記憶
  - `ExtractMemoryElementsAsync()` - 提取實體和關係

#### MemoryExtractionBackgroundService (`Services/MemoryExtractionBackgroundService.cs`)
- **功能**: 背景處理記憶提取佇列
- **特點**: 使用 Channel 實現高效的非阻塞佇列處理

### 3. 對話整合 ✅

#### ChatProcessorService 更新
- **對話前**: 智能檢索相關記憶並注入上下文
- **對話後**: 將對話加入記憶提取佇列進行背景處理
- 記憶檢索失敗不影響正常對話功能

### 4. 記憶管理命令 ✅

#### MemoryCommands (`Commands/MemoryCommands.cs`)
提供完整的記憶管理命令群組：

- `/memory save <content>` - 手動標記重要內容以記憶
- `/memory recall <query>` - 查詢記憶圖譜
- `/memory list` - 列出您的記憶索引
- `/memory clear <scope>` - 清除記憶圖譜（當前伺服器/所有）
- `/memory stats` - 查看記憶統計資訊

### 5. 服務註冊 ✅

#### Program.cs 更新
- 註冊 GraphRag.Net 服務
- 註冊自訂記憶服務（Scoped 和 Singleton）
- 註冊背景服務

## 記憶索引命名規則

- **用戶私人記憶（伺服器內）**: `user_{userId}_guild_{guildId}`
- **用戶私人記憶（DM）**: `user_{userId}_dm`
- **伺服器共享記憶**: `guild_{guildId}_shared` (預留，未使用)

## 工作流程

### 對話時的記憶檢索
1. 用戶發送訊息
2. 系統智能判斷是否需要檢索記憶
3. 如需要，從 GraphRag 圖譜中檢索相關記憶
4. 將記憶內容注入 LLM 系統提示
5. LLM 生成回應時能夠參考歷史記憶

### 對話後的記憶提取
1. 對話完成後，系統將對話加入背景處理佇列
2. 背景服務使用 LLM 分析對話內容
3. 判斷是否包含值得記憶的資訊
4. 提取重要實體、關係和事實
5. 儲存到用戶的 GraphRag 圖譜
6. 生成社群和全局摘要以優化檢索

## 技術特點

### 1. 非阻塞設計
- 記憶提取在背景執行，不影響對話回應速度
- 使用 Channel 實現高效佇列

### 2. 錯誤容錯
- 記憶相關操作失敗不會中斷正常對話功能
- 所有記憶操作都有完善的異常處理

### 3. 智能判斷
- 自動識別需要記憶檢索的場景
- 使用 LLM 判斷對話是否值得記憶
- 過濾瑣碎內容，只記憶有意義的資訊

### 4. 隱私保護
- 每個用戶在每個伺服器的記憶完全隔離
- DM 記憶與伺服器記憶分開儲存

### 5. 使用動態類型
- 使用服務定位器模式和動態類型解決 GraphRag.Net 命名空間問題
- 在運行時解析 IGraphService

## 資料庫分離

- 主資料庫: `llmbot.db` (用戶資料、對話記錄、統計等)
- 記憶資料庫: `graphmem.db` (GraphRAG 圖譜資料)

## 配置說明

### GraphRag 配置項
```json
{
  "GraphRag": {
    "OpenAI": {
      "Key": "not-needed",
      "EndPoint": "https://lmstudio.alanlo.org/v1",
      "ChatModel": "gpt-oss-20b",
      "EmbeddingModel": "text-embedding-ada-002"
    },
    "TextChunker": {
      "LinesToken": 100,
      "ParagraphsToken": 1000
    },
    "GraphDBConnection": {
      "DbType": "Sqlite",
      "DBConnection": "Data Source=graphmem.db",
      "VectorConnection": "graphmem.db",
      "VectorSize": 1536
    },
    "GraphSearch": {
      "SearchMinRelevance": 0.5,
      "SearchLimit": 5,
      "NodeDepth": 2,
      "MaxNodes": 50
    },
    "GraphSys": {
      "RetryCounnt": 2
    },
    "MemoryExtraction": {
      "MinConversationLength": 3,
      "EnableAutoExtraction": true,
      "ExtractionPrompt": "Analyze the conversation and extract important facts, relationships, and entities that should be remembered."
    }
  }
}
```

## 使用範例

### 1. 自動記憶（透明）
用戶正常對話，系統自動：
- 檢索相關歷史記憶
- 在對話後提取並儲存新記憶

### 2. 手動儲存記憶
```
/memory save 我喜歡用 Python 寫爬蟲，特別是用 BeautifulSoup 解析網頁
```

### 3. 查詢記憶
```
/memory recall 我喜歡什麼程式語言
```

### 4. 查看統計
```
/memory stats
```

### 5. 清除記憶
```
/memory clear scope:當前伺服器
```

## 測試建議

1. **基本功能測試**
   - 進行多輪對話，包含個人偏好、事實陳述
   - 使用 `/memory stats` 查看記憶是否被建立
   - 使用 `/memory recall` 測試檢索功能

2. **隔離測試**
   - 在不同伺服器測試，確認記憶互不影響
   - 測試 DM 和伺服器頻道的記憶分離

3. **智能檢索測試**
   - 提及之前討論過的內容（如"記得我之前說的嗎"）
   - 確認系統能自動檢索並使用相關記憶

4. **效能測試**
   - 測試對話回應速度不受記憶系統影響
   - 觀察背景記憶提取不阻塞新對話

## 已知限制

1. **GraphRag.Net API 限制**
   - 使用動態類型處理，可能失去編譯時類型檢查
   - 某些配置選項可能需要進一步調整

2. **記憶品質**
   - 依賴 LLM 判斷，可能偶爾誤判
   - 可透過調整 `MemoryExtraction.ExtractionPrompt` 優化

3. **向量模型**
   - 需要確保 embedding 模型可用且正確配置

## 未來改進方向

1. 支援記憶視覺化（生成圖譜圖片）
2. 支援記憶匯出功能
3. 記憶重要性評分和自動清理
4. 跨伺服器的用戶全局記憶（可選）
5. 記憶分享和協作功能

## 結論

GraphRAG 自動記憶系統已完整實作並成功構建，提供了智能、高效、隔離良好的對話記憶功能。系統設計注重效能和用戶體驗，所有記憶操作都不會影響正常對話功能。

