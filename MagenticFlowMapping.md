## Magentic-UI 交互流程對應

本文件用於快速對照 Magentic-UI 的互動行為與目前 Discord Bot 需要支援的功能，作為後續資料層與服務設計的依據。

### 1. 共規劃 (Co-Planning)

- **Magentic-UI 行為**：使用者在 Web 介面中閱讀/編輯計畫草稿，並可將步驟儲存進 Plan Gallery。
- **Discord 對應**：
  - `/task plan start`：建立 `TaskSession`，記錄初始需求、allowed_websites、approval_policy 等欄位。
  - `/task plan add-step`、`/task plan list`：維護 `TaskPlanStep` 順序與狀態，提供互動式按鈕調整。
  - 以 Embed 呈現「草稿 / READY / EXECUTING」狀態，模擬 Magentic-UI 的 Plan 面板。

### 2. 共執行 (Co-Tasking)

- **Magentic-UI 行為**：Orchestrator 逐步執行並允許使用者插話、要求澄清。
- **Discord 對應**：
  - `TaskOrchestrationService` 會以狀態機推進步驟，並透過 `ChatProcessorService` 串流輸出。
  - 使用 Discord Buttons 讓使用者插入「Pause / Modify / Resume」，對應 Web UI 的控制。

### 3. Action Guard

- **Magentic-UI 行為**：需使用者批准的動作會彈出審批視窗。
- **Discord 對應**：
  - `TaskPlanStep.RequiresApproval` 控制是否要寫入 `ActionApprovalLog`。
  - Bot 發送互動式按鈕（Approve / Reject / Request Edit），回應後更新 `ActionApprovalLog.Status` 與步驟狀態。
  - 容錯：若逾時未批示，狀態切換為 `WAITING_INPUT` 以提醒重新審批。

### 4. Plan Learning / Retrieval

- **Magentic-UI 行為**：歷史計畫可重複使用。
- **Discord 對應**：
  - `TaskSession` 保留 `PlanSnapshot`、`MemoryControllerKey`，並提供 `/task plan reuse` 指令拉出已完成的流程。
  - 後續版本可串 `GraphMemoryService` 以檢索關聯計畫。

### 5. Tell Me When / Monitoring

- **Magentic-UI 行為**：長期監控任務觸發提醒。
- **Discord 對應**：
  - `MonitoredTask` 記錄監控型任務類別、條件、下一次檢查時間。
  - 建立背景服務輪詢 `MonitoredTask`，完成後在頻道回報並更新 `TaskSession.Status = MonitoringCompleted`。

### 6. Slash Command 與 UI 映射

| Magentic-UI 元件 | Discord 元素 |
| --- | --- |
| Plan Editor | `/task plan *` 指令 + Embed/Modal |
| Action Approval Modal | 按鈕 + Modal，寫入 `ActionApprovalLog` |
| Execution Timeline | Thread/Follow-up 訊息，串流回覆 |
| Monitoring Dashboard | `/task monitor list` + 週期性通知 |

此 mapping 將直接驅動資料模型與服務設計，確保 Discord 互動符合 Magentic-UI 的人機流程。

