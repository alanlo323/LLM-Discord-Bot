# 個性化設定與習慣學習功能

## 概述

Bot 現在能夠學習並適應每位用戶的使用習慣，提供個性化的回答體驗。

## 功能特點

### 📊 自動習慣追蹤

Bot 會自動追蹤以下使用習慣：
- 總互動次數
- 連續使用天數
- 平均訊息長度
- 常用命令
- 常用主題類別
- 互動時間模式

### ⚙️ 個人偏好設定

用戶可以自訂以下偏好：

#### 一般設定
- **偏好語言** - 指定回答的語言
- **溫度值** - 控制回答的創意性（0.0-2.0）
- **最大 Token 數** - 控制回答的長度
- **回答風格** - 簡潔、詳細、輕鬆、正式、技術性、創意性
- **自訂系統提示** - 個人化的指示

#### 內容偏好
- **程式碼範例** - 是否偏好在回答中包含程式碼
- **逐步教學** - 是否偏好詳細的逐步說明
- **視覺內容** - 是否偏好視覺化描述

#### 智慧功能
- **智慧建議** - 根據習慣提供個性化建議
- **記憶對話上下文** - 是否記住先前的對話

## 使用命令

### 查看偏好設定

```
/preferences view
```

顯示您當前的所有個人偏好設定和使用統計。

### 設定偏好語言

```
/preferences set-language language:zh-TW
```

設定您偏好的回答語言。

### 設定生成溫度

```
/preferences set-temperature temperature:0.7
```

設定生成溫度（0.0 = 更一致，2.0 = 更創意）。

### 設定最大 Token 數

```
/preferences set-max-tokens max-tokens:2000
```

設定回答的最大長度。

### 設定回答風格

```
/preferences set-style style:詳細
```

選項：簡潔、詳細、輕鬆、正式、技術性、創意性

### 設定自訂提示

```
/preferences set-custom-prompt prompt:請用程式設計師的角度回答問題
```

添加您自己的個性化指示。

### 切換選項

```
/preferences toggle-code-examples
/preferences toggle-step-by-step
```

開關特定的內容偏好。

### 重置設定

```
/preferences reset
```

將所有偏好設定重置為預設值（保留使用統計）。

### 查看使用統計

```
/preferences stats
```

查看詳細的使用統計和習慣分析，包括：
- 基本統計（互動次數、連續天數）
- 活動分析（回應時間、回應長度）
- 近 7 天活動
- 常用命令
- 主題分布

## 自動主題檢測

Bot 會自動檢測您的問題屬於哪個類別：

- 📝 **programming** - 程式設計相關
- 🔢 **math_science** - 數學和科學
- ✍️ **writing_language** - 寫作和語言
- 💼 **business_finance** - 商業和財務
- 🎨 **creative** - 創意和設計
- 📚 **education** - 學習和教育
- 💬 **general** - 一般對話

## 智慧建議

根據您的使用習慣，Bot 會提供智慧建議：

- 使用 10 次後建議設定回答風格
- 連續使用 7 天里程碑慶祝
- 根據訊息長度建議啟用逐步教學
- 根據常用主題建議相關功能

## 個性化對話體驗

當您設定偏好後，Bot 會自動：

1. **應用您的風格偏好** - 根據您選擇的風格調整回答方式
2. **包含您要的內容** - 自動包含程式碼範例或逐步說明
3. **使用您偏好的溫度** - 根據您的設定調整創意程度
4. **應用自訂提示** - 將您的個人指示納入每次對話

## 資料隱私

- 所有偏好設定和習慣資料僅儲存在本地資料庫
- 資料僅用於改善您的個人使用體驗
- 您可以隨時重置或清除您的偏好設定

## 範例場景

### 場景 1：程式設計師

```
/preferences set-style style:技術性
/preferences set-language language:en-US
/preferences toggle-code-examples
/preferences set-custom-prompt prompt:Focus on clean code and best practices
```

結果：獲得技術性、包含程式碼範例的英文回答。

### 場景 2：學生

```
/preferences set-style style:詳細
/preferences toggle-step-by-step
/preferences set-custom-prompt prompt:請用簡單易懂的方式解釋，適合初學者
```

結果：獲得詳細的逐步教學式回答。

### 場景 3：商務人士

```
/preferences set-style style:簡潔
/preferences set-temperature temperature:0.3
/preferences set-custom-prompt prompt:提供專業、直接的商業建議
```

結果：獲得簡潔、一致的專業回答。

## 技術細節

### 資料模型

- **UserPreferences** - 儲存用戶偏好設定
- **InteractionLog** - 記錄每次互動的詳細資訊

### 學習機制

1. **互動記錄** - 每次對話後自動記錄
2. **習慣更新** - 即時更新用戶習慣統計
3. **主題檢測** - 基於關鍵字的智慧主題分類
4. **偏好應用** - 在生成回答前應用個性化設定

### 優先級層級

系統提示優先級（由低到高）：
1. 全域系統提示
2. 伺服器專屬提示
3. 用戶偏好風格
4. 用戶自訂提示

參數優先級：
1. 用戶個人偏好
2. 伺服器設定限制
3. 全域設定限制

## 未來改進

計劃中的功能：
- 基於時間的偏好（例如工作日 vs 週末）
- 更精確的主題分類（機器學習模型）
- 協作過濾推薦
- 跨設備同步偏好設定

