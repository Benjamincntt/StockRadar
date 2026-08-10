# Spec — LLM veto Telegram VIP Buy (phương án A)

**Status:** Implemented  
**Scope:** Sau rule+ML PASS, trước `DispatchAsync` BuyPoint — ShopAIKey Claude `ALLOW`/`BLOCK`.  
**Related:** [`vip-intraday-ml-accuracy/spec.md`](./vip-intraday-ml-accuracy/spec.md), [`../../domain/buy-decision.md`](../../domain/buy-decision.md).

## Behavior

1. Rule + ML + anti-spam xác nhận BuyPoint1/2.
2. `VipLlmContextBuilder` ghép hồ sơ đầy đủ (Top + quote + ML + orderflow + `GetDetailAsync`).
3. Anthropic Messages (`POST {ApiBaseUrl}/v1/messages`) → JSON `{decision, reason}`.
4. `BLOCK` + `ShadowMode=false` → không Telegram / không mở vị thế; vẫn ghi `VipAlertFires` (Llm*).
5. `ShadowMode=true` → log + vẫn bắn (đo trước).
6. Timeout/lỗi → `FailOpen` (mặc định ALLOW).

## Config `VipLlmJudge`

```json
{
  "Enabled": true,
  "ApiBaseUrl": "https://api.shopaikey.com",
  "ApiKey": "<shopaikey>",
  "Model": "claude-haiku-4-5-20251001",
  "TimeoutMs": 8000,
  "MaxHistoryBars": 120,
  "FailOpen": true,
  "ShadowMode": true
}
```

Secret chỉ trên server / `appsettings.Production.json` (gitignore).

## Files

- `VipLlmJudgeOptions`, `IVipLlmJudge`
- `AnthropicVipLlmJudge`, `VipLlmContextBuilder`, `VipLlmJudgeParsing`
- `TopOpportunityVipAlertPublisher` (wire)
- Migration LLM columns trên `VipAlertFires`
