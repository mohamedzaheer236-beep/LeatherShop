# WhatsApp Business Setup — Cuir Galerie

Complete record of the WhatsApp Business setup for the Cuir Galerie API, including what was done, current status, and what's pending.

---

## Current Setup Status

| Component | Status | Details |
|-----------|--------|---------|
| **Business Portfolio** | ✅ Created | "Leathershop" (ID: YOUR_PORTFOLIO_ID) |
| **Meta App** | ✅ Created | "Cuir Galerie" (ID: YOUR_META_APP_ID) |
| **System User** | ✅ Created | "Leathershop" (Admin, ID: YOUR_SYSTEM_USER_ID) under Cuir Galerie portfolio |
| **Permanent Token** | ✅ Generated | Never-expiring token with `whatsapp_business_messaging` + `whatsapp_business_management` permissions |
| **WABA** | ✅ Created | WhatsApp Business Account ID: YOUR_WABA_ID |
| **Phone Number** | ✅ Added & Registered | +91 84386 29975 (Phone Number ID: YOUR_PHONE_NUMBER_ID) |
| **Phone Registration** | ✅ Registered | Via Cloud API `/register` endpoint with PIN 123456 |
| **Business Verification** | ❌ Not Done | "Cuir Galerie" portfolio not yet verified by Meta |
| **Payment Method** | ❌ Not Added | Required for sending messages beyond free tier |
| **Webhook** | ❌ Not Configured | Needs to be set up once API is deployed or using ngrok |
| **Template Approval** | ⏳ Pending | All 3 custom templates awaiting Meta review |

---

## Credentials (in appsettings.json)

```json
"WhatsApp": {
    "PhoneNumberId": "YOUR_PHONE_NUMBER_ID",
    "BusinessAccountId": "YOUR_WABA_ID",
    "AccessToken": "<SECRET — stored in appsettings.Local.json / Railway env vars>",
    "VerifyToken": "REDACTED_VERIFY_TOKEN",
    "AppSecret": "<SECRET — stored in appsettings.Local.json / Railway env vars>",
    "ApiVersion": "v22.0"
}
```

| Field | Value | Source |
|-------|-------|--------|
| `PhoneNumberId` | YOUR_PHONE_NUMBER_ID | Meta Developer Console → WhatsApp → API Setup |
| `BusinessAccountId` | YOUR_WABA_ID | WABA ID from Meta Business Suite |
| `AccessToken` | Permanent System User token | Generated from "Leathershop" system user |
| `VerifyToken` | `REDACTED_VERIFY_TOKEN` | Custom string — must match webhook configuration |
| `ApiVersion` | `v22.0` | WhatsApp Cloud API version |

---

## Message Templates

### Created Templates

| Template Name | Category | Template ID | Status | Body |
|---------------|----------|-------------|--------|------|
| `shop_deals` | MARKETING | 2107912596695779 | ⏳ PENDING | `🛍️ New deals at Cuir Galerie! {{1}} Check out our latest collection. Shop now!` |
| `order_update` | UTILITY | 1636258954059739 | ⏳ PENDING | `📦 Order Update: Your order {{1}} status is now: {{2}}. Thank you for shopping with us!` |
| `store_notification` | UTILITY | 2317291185767700 | ⏳ PENDING | `📢 {{1}}` |
| `hello_world` | UTILITY | 1132494892234892 | ✅ APPROVED | Default Meta template (⚠️ only works with test phone numbers, NOT real numbers) |

### Template Notes

- **All custom templates are PENDING Meta approval** — cannot send broadcasts until approved
- `hello_world` returns error `"Hello World templates can only be sent from the Public Test Numbers"` when used with real phone +91 84386 29975
- `shop_deals` replaces a deleted `shop_offer` template that had duplicated body text
- `store_notification` (UTILITY) was created as a fallback — UTILITY templates typically get approved faster than MARKETING
- Once approved, the broadcast page's Quick Message and Template message features will work

### How Templates Are Used in the App

1. **Broadcast Page → Template Message tab**: Admin selects from approved templates, enters parameters, selects recipients, and sends
2. **Broadcast Page → Quick Message tab**: Uses `shop_deals` template with a textarea for the message body (passed as parameter `{{1}}`)
3. **Customers Page → Send to Selected**: Opens a dialog to select a template and send to checked customers

---

## Architecture

```
Customer (WhatsApp) ──→ Meta Cloud API ──→ Webhook (POST) ──→ .NET API ──→ ChatBotService
                   ←── Meta Cloud API ←── WhatsAppService ←── .NET API ←── (response)
                   
Admin Panel ──→ BroadcastController ──→ BroadcastService (enqueue) ──→ Channel<T>
                                                                         ↓
                                                            BroadcastBackgroundService
                                                                         ↓
                                                            WhatsAppService.SendTemplateMessage()
                                                            (10 concurrent sends via SemaphoreSlim)
```

---

## What's Done

### 1. System User & Permanent Token
- Created Admin System User "Leathershop" under "Cuir Galerie" Business Portfolio
- Generated permanent (non-expiring) access token with permissions:
  - `whatsapp_business_messaging` — send/receive messages
  - `whatsapp_business_management` — manage templates, phone numbers
- Token stored in `appsettings.json` → `WhatsApp:AccessToken`

### 2. Phone Number Registration
- Added phone +91 84386 29975 to WABA YOUR_WABA_ID
- Registered the phone number via WhatsApp Cloud API `/register` endpoint with PIN 123456
- Phone status: **Connected**

### 3. Message Templates
- Created 3 custom templates via Meta Graph API (`POST /v22.0/{WABA_ID}/message_templates`)
- All submitted successfully and assigned template IDs
- Awaiting Meta review/approval

### 4. Broadcast System
- Backend: `BroadcastService` enqueues jobs → `BroadcastBackgroundService` processes with `Channel<T>` + `SemaphoreSlim(10)` concurrency
- Frontend: Two-tab broadcast page (Quick Message / Template Message) with recipient selection and history
- Status polling: `GET /api/broadcast/{id}/status` — frontend polls every 1s for up to 30s for real-time sent/failed counts
- Custom status banners (sending/success/error) with animations

---

## What's Pending

### Must Do Before Production

| # | Task | Details |
|---|------|---------|
| 1 | **Wait for template approval** | All 3 custom templates need Meta approval before broadcasts work. Check status at: Meta Business Suite → WhatsApp Manager → Message Templates |
| 2 | **Add payment method** | Go to Meta Business Suite → Payment Settings → add credit card. Required to send messages beyond the free conversation tier (1,000 free service conversations/month) |
| 3 | **Verify "Cuir Galerie" business** | Business verification required for: higher messaging limits, payment configuration access, green/blue tick. Go to: Meta Business Suite → Settings → Business Info → Start Verification |
| 4 | **Configure webhook** | Meta Developer Console → WhatsApp → Configuration → set Callback URL to your API endpoint + Verify Token. Subscribe to `messages` field |
| 5 | **Webhook signature validation** | Validate `X-Hub-Signature-256` header on incoming webhook POSTs to prevent spoofed payloads |
| 6 | **Move secrets to environment variables** | WhatsApp token, DB password, JWT key should NOT be in `appsettings.json`. Use User Secrets (dev) or env vars (prod) |

### Nice to Have

| # | Task | Details |
|---|------|---------|
| 7 | **Server-side pagination** | API endpoints currently return all records. Add `?page=1&pageSize=25` for scale |
| 8 | **WhatsApp retry with rate limiting** | Add per-message delay and retry logic in BroadcastBackgroundService for WhatsApp API rate limits |
| 9 | **Product images in chatbot** | ChatBot currently sends text-only product details. Could use WhatsApp media messages |
| 10 | **Customer address collection** | Add chatbot flow to ask for shipping address before checkout |

---

## WhatsApp Webhook Setup (Step-by-Step)

### For Development (ngrok)

1. Start your API: `cd LeatherShopAPI && dotnet run`
2. Start ngrok: `ngrok http 5000`
3. Copy the HTTPS URL (e.g., `https://abc123.ngrok-free.app`)
4. Go to [developers.facebook.com](https://developers.facebook.com/) → your app → WhatsApp → Configuration
5. Click **Edit** under Webhook:
   - Callback URL: `https://abc123.ngrok-free.app/api/whatsapp/webhook`
   - Verify token: `REDACTED_VERIFY_TOKEN`
6. Click **Verify and Save**
7. Under Webhook fields → subscribe to **messages**
8. Test by sending "Hi" to +91 84386 29975 from any WhatsApp

### For Production

1. Deploy your API to a cloud platform (Railway, Azure, etc.)
2. Use the deployed URL as the Callback URL
3. Same verify token as above
4. API must be **always running** — Meta retries webhooks for a limited time

---

## Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| Broadcasts show "all failed" | Templates not yet approved | Wait for Meta approval or use an approved template |
| `hello_world` errors | "Hello World templates can only be sent from Public Test Numbers" | Use custom approved templates instead |
| 401 from Meta API | Token invalid or expired | Regenerate from System User settings (should be permanent) |
| Webhook verification fails | Verify token mismatch | Check `WhatsApp:VerifyToken` in appsettings.json matches Meta Configuration |
| Messages not received | Webhook not configured or `messages` field not subscribed | Check Meta Developer Console → Configuration |
| Can't access payment configs | Business not verified | Complete business verification for "Cuir Galerie" portfolio |
| Low messaging limits | Need to verify business or add payment method | Complete verification + add payment method for higher tiers |

---

## Business Portfolios Reference

| Portfolio | ID | Verified | Notes |
|-----------|-----|----------|-------|
| Bovino | (original) | ✅ Verified (May 2024) | Original portfolio — 7-day system user age restriction |
| Leathershop | YOUR_PORTFOLIO_ID | ❌ Not verified | New portfolio with WABA + WhatsApp setup. Needs verification |

---

## Messaging Limits & Costs

| Tier | Limit | How to Unlock |
|------|-------|---------------|
| **Unverified business** | 250 business-initiated conversations/day | Default |
| **Verified business** | 1,000 → 10,000 → 100,000/day (auto-scales) | Complete business verification |
| **Free tier** | 1,000 free service conversations/month | Automatic |
| **Paid conversations** | ~₹0.30-0.70 per conversation (varies by category) | Add payment method |

> **Important:** Each "conversation" is a 24-hour window with one customer, not per message. You can send unlimited messages within a conversation window.
