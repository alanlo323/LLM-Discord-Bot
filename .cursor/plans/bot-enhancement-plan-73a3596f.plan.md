<!-- 73a3596f-439f-4835-b7bd-2d07fe726718 6c1d9548-eaad-4f45-9be3-4f407b6fca5b -->
# LLM Discord Bot 功能增強計劃

## 第一階段：高優先級功能

### 1. 多模態支持（圖片理解）

**目標**：允許用戶上傳圖片並讓 LLM 分析內容

**實作內容**：

- 修改 `ChatCommands.cs` 的 `/chat` 命令，增加 `attachment` 參數
- 擴展 `LLMService.cs` 支援圖片作為輸入
- 實作圖片下載和 Base64 編碼
- 配置支援視覺的模型（GPT-4V、Claude 3 等）
- 增加圖片大小和格式限制檢查

**影響檔案**：

- `Commands/ChatCommands.cs`
- `Services/LLMService.cs`
- `Configuration/BotConfig.cs`

### 2. 對話模板系統

**目標**：提供預設的對話場景模板

**實作內容**：

- 建立 `ConversationTemplate` 資料模型
- 資料庫 Migration 增加 `ConversationTemplates` 表
- 建立 `TemplateCommands.cs` 命令群組
- `/template list` - 列出所有模板
- `/template use <name>` - 使用模板開始對話
- `/template create` - 建立自訂模板（管理員）
- `/template delete` - 刪除模板（管理員）
- 預設模板：程式碼審查、翻譯、寫作、數學輔導、創意腦力激盪

**新增檔案**：

- `Models/ConversationTemplate.cs`
- `Commands/TemplateCommands.cs`
- `Migrations/[timestamp]_AddConversationTemplates.cs`

### 3. 對話匯出功能

**目標**：讓用戶能匯出聊天記錄

**實作內容**：

- 在 `UserCommands.cs` 增加 `/export` 命令
- 實作多種匯出格式：
- Markdown (.md)
- JSON (.json)
- HTML (美觀的網頁格式)
- 使用 Discord 的附件功能傳送檔案
- 增加日期範圍篩選選項

**影響檔案**：

- `Commands/UserCommands.cs`
- `Services/ExportService.cs`（新建）

### 4. 知識庫/RAG 系統

**目標**：為伺服器提供自訂知識庫

**實作內容**：

- 整合向量資料庫（Qdrant 或 Chroma）
- 建立 `KnowledgeBase` 和 `KnowledgeDocument` 模型
- 實作文件上傳和處理（PDF、TXT、MD）
- 文件分塊和向量化
- 建立 `KnowledgeCommands.cs`
- `/knowledge upload` - 上傳文件
- `/knowledge list` - 列出知識庫文件
- `/knowledge delete` - 刪除文件
- `/knowledge search` - 搜索知識庫
- 在對話時自動檢索相關知識
- 建立 `RagService.cs` 處理檢索增強生成

**新增檔案**：

- `Models/KnowledgeBase.cs`
- `Models/KnowledgeDocument.cs`
- `Services/RagService.cs`
- `Services/VectorStoreService.cs`
- `Commands/KnowledgeCommands.cs`

## 第二階段：中優先級功能

### 5. 多模型切換

**實作內容**：

- 擴展 `BotSettings` 支援多個模型配置
- `/chat` 命令增加 `model` 參數
- 顯示可用模型列表和特性
- 追蹤不同模型的使用統計

### 6. 對話主題管理

**實作內容**：

- `ChatHistory` 增加 `TopicId` 和 `TopicName` 欄位
- 建立 `TopicCommands.cs`
- 實作主題切換、建立、刪除、列表功能

### 7. 自動摘要功能

**實作內容**：

- 建立 `/summarize` 命令
- 實作對話摘要生成邏輯
- 儲存摘要記錄
- 定期自動摘要選項

### 8. 擴展外部工具插件

**實作內容**：

- 天氣查詢插件（OpenWeatherMap）
- 計算器插件
- 新聞查詢插件
- 股票價格查詢插件
- 統一的 Plugin 介面設計

### 9. 反饋與評分系統

**實作內容**：

- 在回應訊息加入反應按鈕（👍/👎）
- 建立 `Feedback` 模型儲存反饋
- 管理員可查看反饋統計
- 基於反饋優化提示詞

### 10. GraphRAG 自動記憶系統

**目標**：使用圖資料庫自動學習和記憶對話中的有意義內容

**實作內容**：

- 整合 GraphRag.Net 套件
- 建立雙層記憶架構：
- 用戶私人記憶圖譜（個人化知識）
- 伺服器共享記憶圖譜（團隊知識庫）
- 自動內容分析和記憶提取：
- 背景服務監控對話內容
- 使用 LLM 識別重要實體、關係、事件
- 自動建立知識圖譜節點和邊
- 設定記憶觸發條件（例如：定義、事實陳述、重要決策）
- 手動記憶管理：
- `/memory save <content>` - 手動標記重要內容
- `/memory recall <query>` - 查詢記憶圖譜
- `/memory list` - 列出記憶節點
- `/memory delete <id>` - 刪除特定記憶
- `/memory graph` - 視覺化記憶圖譜（生成圖片）
- 建立 `GraphMemoryService.cs` 處理圖譜操作
- 建立 `MemoryAnalyzerService.cs` 分析對話內容並提取記憶
- 在對話時自動從記憶圖譜中檢索相關上下文
- 權限控制：用戶只能訪問自己的私人記憶，伺服器管理員可管理共享記憶

**與傳統 RAG 的區別**：

- 傳統 RAG（第一階段第4項）：結構化文件知識庫，向量相似度檢索
- GraphRAG：動態學習的知識圖譜，關係推理和多跳查詢

**新增檔案**：

- `Models/MemoryNode.cs`
- `Models/MemoryRelation.cs`
- `Models/UserMemoryGraph.cs`
- `Models/GuildMemoryGraph.cs`
- `Services/GraphMemoryService.cs`
- `Services/MemoryAnalyzerService.cs`
- `Commands/MemoryCommands.cs`
- `Migrations/[timestamp]_AddGraphMemoryTables.cs`

## 第三階段：進階功能

### 11. Discord UI 組件強化

**實作內容**：

- 使用 Button、SelectMenu、Modal 等互動組件
- 建立更友善的命令流程
- 快速操作按鈕集

### 12. 進階統計與視覺化

**實作內容**：

- 整合圖表生成（Chart.js 或 QuickChart）
- 生成統計圖表圖片
- 使用趨勢分析
- 成本預測

### 13. 個性化設定

**實作內容**：

- 用戶偏好設定（語言、風格、溫度）
- 記憶用戶習慣
- `/preferences` 命令群組

### 14. 內容審核與安全

**實作內容**：

- 敏感內容過濾
- 速率限制強化
- 垃圾訊息偵測

## 技術考量

### 依賴套件

- **向量資料庫**：Qdrant.Client 或 Microsoft.SemanticKernel.Connectors.Memory.Qdrant
- **GraphRAG**：GraphRag.Net
- **文件處理**：iTextSharp (PDF)、Markdig (Markdown)
- **圖片處理**：System.Drawing.Common 或 SixLabors.ImageSharp
- **圖表生成**：ScottPlot 或使用 QuickChart API

### 資料庫變更

需要建立多個 EF Core Migrations：

- ConversationTemplates
- KnowledgeBase & KnowledgeDocuments
- Topics (修改 ChatHistory)
- Feedback
- UserPreferences
- GraphMemory (MemoryNodes, MemoryRelations, UserMemoryGraphs, GuildMemoryGraphs)

### 效能考量

- 圖片處理可能耗時，需要適當的超時處理
- 向量搜索應該異步執行
- 大型文件處理應使用背景工作
- GraphRAG 記憶提取應在背景執行，不阻塞對話回應
- 考慮增加快取機制
- 記憶圖譜查詢需要優化（索引、限制深度）

## 下一步

1. 確認要實作的功能優先順序
2. 審查技術選型（特別是向量資料庫和 GraphRag.Net）
3. 開始第一階段實作