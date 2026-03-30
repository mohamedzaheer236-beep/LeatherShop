# WhatsApp Business Setup — Cuir Galerie

Complete record of the WhatsApp Business setup for Cuir Galerie. This reflects the **current production state** as of March 2026.

---

## Current Setup Status

| Component | Status | Details |
|-----------|--------|---------|
| **Business Portfolio** | ✅ Active | "Leathershop" |
| **Meta App** | ✅ Active | "Cuir Galerie" |
| **System User** | ✅ Active | "Leathershop" (Admin) under Cuir Galerie portfolio |
| **Permanent Token** | ✅ Active | Never-expiring token with `whatsapp_business_messaging` + `whatsapp_business_management` |
| **WABA** | ✅ Active | WhatsApp Business Account configured |
| **Phone Number** | ✅ Registered | Registered via Cloud API |
| **Business Verification** | ✅ Complete | "Cuir Galerie" verified by Meta |
| **Webhook** | ✅ Active | Deployed on Railway, subscribed to `messages` field |
| **Webhook Signature** | ✅ Validated | `X-Hub-Signature-256` verified on every incoming POST |
| **Secrets** | ✅ Secured | All secrets in Railway environment variables, not in code |
| **Templates** | ✅ Multiple Approved | See template table below |

---

## Credentials (in appsettings.json keys — values in Railway env vars)

```json
"WhatsApp": {
    "PhoneNumberId": "<RAILWAY_ENV>",
    "BusinessAccountId": "<RAILWAY_ENV>",
    "AccessToken": "<RAILWAY_ENV>",
    "VerifyToken": "<RAILWAY_ENV>",
    "AppSecret": "<RAILWAY_ENV>",
    "ApiVersion": "v22.0"
}
```

| Field | Source |
|-------|--------|
| `PhoneNumberId` | Meta Developer Console → WhatsApp → API Setup |
| `BusinessAccountId` | WABA ID from Meta Business Suite |
| `AccessToken` | Permanent System User token |
| `VerifyToken` | Custom string — must match webhook configuration |
| `ApiVersion` | `v22.0` (WhatsApp Cloud API) |

---

## Message Templates

| Template Name | Category | Status | Usage | Body |
|---------------|----------|--------|-------|------|
| `shop_deals` | MARKETING | ✅ **APPROVED** | Quick Message tab; 1 param `{{1}}` = message body | `New deals at Leather Shop! {{1}} Check out our latest collection. Shop now!` |
| `order_update` | UTILITY | ✅ **APPROVED** | Auto-sent when admin updates order status; 2 params | `📦 Order Update: Your order {{1}} status is now: {{2}}. Thank you for shopping with us!` |
| `store_notification` | UTILITY | ✅ **APPROVED** | Used for welcome messages on customer creation; 1 param | `📢 {{1}}` |
| `hello_world` | UTILITY | ✅ **APPROVED** | Default Meta template — only works with test numbers | `Hello World!` |
| `single_product` | MARKETING | ✅ **APPROVED** | IMAGE header; 3 params: product name, price, description | Custom product showcase |
| `single_product_v2` | MARKETING | ✅ **APPROVED** | Updated IMAGE header; 3 params: product name, price, description. Used in production. | Custom product showcase |
| `product_gallery` | MARKETING (Carousel) | ✅ **APPROVED** | 2-card product carousel | Variable per card |
| `product_gallery_3` | MARKETING (Carousel) | ✅ **APPROVED** | 3-card product carousel | Variable per card |
| `product_gallery_4` | MARKETING (Carousel) | ✅ **APPROVED** | 4-card product carousel | Variable per card |
| `customer_welcomemsg` | MARKETING | ✅ **APPROVED** | Created as UTILITY, approved as MARKETING by Meta. Superseded by `store_notification`. | Onboarding welcome |
| `custom_message` | MARKETING | ⏳ **PENDING** | General-purpose broadcast; 1 param; branded header+footer | `📢 *Cuir Galerie*\n\n{{1}}\n\nReply Hi to explore and shop on WhatsApp!` |
| `luxury_discover` | MARKETING | ⏳ **PENDING** | Fixed promotional message — no params; store launch promo | Fixed body text |

### Important: Newline Restriction (Meta Error 132018)
Meta rejects template parameter values containing newline (`\n`), tab (`\t`), or 4+ consecutive spaces. The backend `SanitizeParam()` method in `WhatsAppService.cs` automatically strips newlines to spaces before sending. This means all broadcast messages arrive as a single flat paragraph — this is a **Meta platform constraint with no workaround**.

### How Templates Are Used in the App

1. **Broadcast Page → Template Message tab**: Admin selects from approved MARKETING templates, enters parameters, selects recipients, and sends
2. **Broadcast Page → Quick Message tab**: Uses `shop_deals` template — textarea content passed as `{{1}}`
3. **Customers Page → Send to Selected**: Opens broadcast dialog; category-targeted or phone-list sends
4. **Customer Creation**: `CustomerService.CreateAsync()` sends `store_notification` template as welcome (not freeform text — new customers have no active 24h window)
5. **Order Status Updates**: `OrderService` sends `order_update` template when admin changes order status

> **System templates** (`customer_welcomemsg`, `store_notification`) are filtered out of the broadcast dropdown — only MARKETING templates appear there.

---

## Architecture

```
Customer (WhatsApp) ──→ Meta Cloud API ──→ Webhook (POST) ──→ .NET API ──→ ChatBotService
                   ←── Meta Cloud API ←── WhatsAppService ←── .NET API ←── (response)
                   
Admin Panel ──→ BroadcastController ──→ BroadcastService (enqueue) ──→ Channel<int>
                                                                         ↓
                                                            BroadcastBackgroundService
                                                                         ↓
                                                            WhatsAppService.SendTemplateMessage()
                                                            (10 concurrent sends via SemaphoreSlim)
                                                            Progress saved every 50 messages
                                                            Resumes on API restart from last checkpoint
```

---

## Broadcast System Details

| Feature | Details |
|---------|---------|
| **Concurrency** | `SemaphoreSlim(10)` — 10 parallel sends, 200ms delay between batches |
| **Rate** | ~50 messages/second |
| **Progress** | Saved to DB every 50 messages — survives API restarts |
| **Recipient targeting** | All subscribers / Subscribers by category / Explicit phone list |
| **History** | All broadcasts logged in `BroadcastMessages` table |
| **Status polling** | Frontend polls `GET /api/broadcast/{id}/status` every 1s during send |

---

## ChatBot Handlers (Incoming Messages)

| Handler | Trigger | Action |
|---------|---------|--------|
| `MenuHandler` | "hi", "hello", "hey", "start", "menu", "help" | Sends welcome message + main menu with interactive buttons |
| `ProductHandler` | "browse_{category}", "cat_{category}", product selection | Sends product carousel by category, product details with images |
| `CartHandler` | "add_cart_{productId}", "view_cart", "remove_cart" | Add/remove/view cart items, quantity input flow |
| `CheckoutHandler` | "checkout" | Address collection flow, order placement, Paytm payment link |
| `OrderHistoryHandler` | "orders", "my orders" | Shows recent orders with status details |
| `OrderCancellationHandler` | "cancel_order_{orderId}" | Customer-initiated cancellation of unpaid Pending orders, restores cart items |
| `ContactHandler` | "contact", "support" | Shows business contact info (phone, WhatsApp link, hours, services) |

---

## WhatsApp Webhook Setup

### For Development (ngrok)

1. Start your API: `cd LeatherShopAPI && dotnet run`
2. Start ngrok: `ngrok http 5000`
3. Copy the HTTPS URL (e.g., `https://abc123.ngrok-free.app`)
4. Go to [developers.facebook.com](https://developers.facebook.com/) → your app → WhatsApp → Configuration
5. Click **Edit** under Webhook:
   - Callback URL: `https://abc123.ngrok-free.app/api/whatsapp/webhook`
   - Verify token: same as `WhatsApp:VerifyToken` in appsettings
6. Click **Verify and Save**
7. Under Webhook fields → subscribe to **messages**
8. Test by sending "Hi" to your WhatsApp Business number

### For Production (Railway)

Webhook is already configured. Callback URL points to Railway deployment.

---

## Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| Broadcast shows all failed | Template not approved or parameter contains `\n` | Wait for approval; check `SanitizeParam()` strips newlines |
| Meta error 132018 | Newline/tab in template parameter value | `SanitizeParam()` handles this automatically |
| `hello_world` errors | Only works with Meta test numbers | Use any other approved custom template |
| 401 from Meta API | Token invalid | Regenerate from System User settings (should be permanent) |
| Webhook verification fails | Verify token mismatch | Check `WhatsApp:VerifyToken` env var matches Meta Configuration |
| Messages not received | Webhook not subscribed to `messages` | Check Meta Developer Console → Configuration → Webhook fields |
| Order notifications not sent | `order_update` template rejection | Verify template is APPROVED in Meta Business Suite |
