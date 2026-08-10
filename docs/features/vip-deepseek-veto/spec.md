# Spec — DeepSeek veto Telegram VIP Buy (phương án A)

**Status:** Implemented  
**Scope:** Sau rule+ML PASS, trước `DispatchAsync` BuyPoint — DeepSeek `ALLOW`/`BLOCK`.  
**Related:** [`vip-intraday-ml-accuracy/spec.md`](./vip-intraday-ml-accuracy/spec.md), [`../../domain/buy-decision.md`](../../domain/buy-decision.md).

## Behavior

1. Rule + ML + anti-spam xác nhận BuyPoint1/2.
2. `VipLlmContextBuilder` ghép hồ sơ đầy đủ:
   - Top opportunity snapshot (SetupDna, BuyScore, EntryPointJson, ExplainJson, phase…)
   - Live quote + paced volume + gain from open
   - Local ML gate (P(hit), MA/ATR/RS, min threshold)
   - Orderflow (foreign/prop/pressure/VSA)
   - **Stock dossier** từ `IStockService.GetDetailAsync`: BuyDecision, Entry, FlatBox, criteria, signals, OHLCV (tối đa `MaxHistoryBars`)
3. DeepSeek Chat Completions → JSON `{decision, reason}`.
4. `BLOCK` + `ShadowMode=false` → không Telegram / không mở vị thế; vẫn ghi `VipAlertFires` (Llm*).
5. `ShadowMode=true` → log + vẫn bắn (đo trước).
6. Timeout/lỗi → `FailOpen` (mặc định ALLOW).

## Config `VipLlmJudge`

```json
{
  "Enabled": true,
  "Provider": "deepseek",
  "ApiBaseUrl": "https://api.deepseek.com",
  "ApiKey": "<secret>",
  "Model": "deepseek-v4-flash",
  "TimeoutMs": 3000,
  "MaxHistoryBars": 120,
  "FailOpen": true,
  "ShadowMode": true
}
```

Bật veto thật: `ShadowMode=false` sau khi đo shadow ổn.

## Files

- `VipLlmJudgeOptions`, `IVipLlmJudge`
- `DeepSeekVipLlmJudge`, `VipLlmContextBuilder`
- `TopOpportunityVipAlertPublisher` (wire)
- Migration `LlmDecision/Reason/LatencyMs/Model/ShadowMode` trên `VipAlertFires`
