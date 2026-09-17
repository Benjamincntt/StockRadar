# Spec — LLM veto Telegram VIP (mua + bán)

**Status:** Implemented  
**Scope:** Sau rule(+ML mua) PASS, trước `DispatchAsync` BuyPoint **và** Sell/RiskWarning — ShopAIKey Claude `ALLOW`/`BLOCK`.  
**Related:** [`vip-intraday-ml-accuracy/spec.md`](./vip-intraday-ml-accuracy/spec.md), [`../../domain/buy-decision.md`](../../domain/buy-decision.md).

## Behavior

1. Rule + ML + anti-spam xác nhận BuyPoint1/2; hoặc rule bán/cảnh báo xác nhận Sell/Risk.
2. `VipLlmContextBuilder` ghép hồ sơ (`BuildAsync` mua / `BuildForPositionAsync` bán).
3. Anthropic Messages (`POST {ApiBaseUrl}/v1/messages`) → JSON `{decision, reason}`.
4. `BLOCK` + `ShadowMode=false` → không Telegram / không mở-đóng vị thế; vẫn ghi `VipAlertFires` (Llm*).
5. `ShadowMode=true` → log + vẫn bắn (đo trước); dòng `AI: …` vẫn hiện trên Telegram.
6. Timeout/lỗi → `FailOpen` (mặc định ALLOW).
7. Entry Ready **không** gọi LLM (chỉ theo dõi vùng vào).

## Config `VipLlmJudge`

```json
{
  "Enabled": true,
  "ApiBaseUrl": "https://api.shopaikey.com",
  "ApiKey": "<shopaikey>",
  "Model": "claude-haiku-4-5-20251001",
  "TimeoutMs": 15000,
  "MaxHistoryBars": 120,
  "FailOpen": true,
  "ShadowMode": true
}
```

Secret chỉ trên server / `appsettings.Production.json` (gitignore).

`TimeoutMs` bị chặn trần trong code: `cts.CancelAfter(Math.Clamp(cfg.TimeoutMs, 500, 15_000))`
(`AnthropicVipLlmJudge.cs`). Đặt config cao hơn 15000 sẽ bị cắt về 15000 và không báo gì.

`TimeoutMs` nâng 8000 → 15000 ngày 16/09/2026. Đo từ log 14 ngày, 4 lần gọi: ba lần trả lời
trong 4.291 / 5.200 / 7.083 ms, một lần chạm ngưỡng ở 8.002 ms. Ngưỡng cũ chỉ cách lần chậm
nhất thành công 0,9 giây. HttpClient cho 20 giây nên 15000 vẫn nằm trong giới hạn. Cỡ mẫu 4
là nhỏ — đây là nới biên, không phải con số rút từ phân phối latency đáng tin.

## Files

- `VipLlmJudgeOptions`, `IVipLlmJudge`
- `AnthropicVipLlmJudge`, `VipLlmContextBuilder`, `VipLlmJudgeParsing`
- `TopOpportunityVipAlertPublisher` (wire mua + bán)
- Migration LLM columns trên `VipAlertFires`
