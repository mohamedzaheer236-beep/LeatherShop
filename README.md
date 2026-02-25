# Leather Shop — WhatsApp Business Ordering System

A complete WhatsApp Business ordering system for a leather goods seller. Customers browse products, add to cart, and pay — all inside WhatsApp. The shop owner manages everything from an Angular admin panel.

**Tech Stack:** Angular 18 · PrimeNG 17 · .NET 8 Web API · Entity Framework Core · PostgreSQL · WhatsApp Cloud API · Razorpay

---

## Table of Contents

1. [What Has Been Built](#what-has-been-built)
2. [How It Works — System Architecture](#how-it-works--system-architecture)
3. [Customer WhatsApp Flow](#customer-whatsapp-flow)
4. [Admin Panel Flow](#admin-panel-flow)
5. [Project Structure](#project-structure)
6. [Developer Setup Guide](#developer-setup-guide)
7. [External Services Setup (WhatsApp, Razorpay)](#external-services-setup)
8. [API Endpoints Reference](#api-endpoints-reference)
9. [Database Schema](#database-schema)
10. [What Is NOT Yet Implemented](#what-is-not-yet-implemented)
11. [Code Audit Report](#code-audit-report)
12. [Deployment Guide](#deployment-guide)

---

## What Has Been Built

### Backend API (.NET 8) — `LeatherShopAPI/`

**Architecture:** Interface → Service (business logic) → Controller (thin, HTTP only). Entity configurations via Fluent API. DI registration with `AddScoped`/`AddHttpClient`.

| Layer | File(s) | What It Does |
|-------|---------|--------------|
| **Middleware** | `Middleware/ExceptionHandlingMiddleware.cs` | Global exception handling — catches all unhandled exceptions, logs them, returns consistent `ApiResponse` JSON. Maps exception types to HTTP status codes (404, 400, 409, 401, 500). Prevents stack trace leaks. |
| **API Response Model** | `Models/ApiResponse.cs` | Unified response envelope `ApiResponse<T>` with `success`, `message`, `data`, `errors` fields. Generic and non-generic versions. All controllers return this shape. |
| **Controllers (thin)** | `AuthController.cs`, `ProductsController.cs`, `OrdersController.cs`, `CustomersController.cs`, `DashboardController.cs`, `BroadcastController.cs`, `PaymentController.cs`, `WhatsAppWebhookController.cs`, `ChatController.cs` | HTTP routing only — delegates all logic to service interfaces. Wraps responses in `ApiResponse<T>`. `[Authorize]` on all admin controllers; Auth/Payment/Webhook are public. |
| **Service Interfaces** | `Services/Interfaces/IProductService.cs`, `IOrderService.cs`, `ICustomerService.cs`, `IDashboardService.cs`, `IBroadcastService.cs`, `IPaymentService.cs`, `IWhatsAppService.cs`, `IChatBotService.cs`, `IChatService.cs` | Contracts for all business logic |
| **Service Implementations** | `Services/ProductService.cs`, `OrderService.cs`, `CustomerService.cs`, `DashboardService.cs`, `BroadcastService.cs`, `PaymentService.cs`, `WhatsAppService.cs`, `ChatBotService.cs`, `ChatService.cs` | All business logic lives here — DB queries, WhatsApp API calls, chatbot state machine, admin chat |
| **Real-time (SignalR)** | `Hubs/NotificationHub.cs` | SignalR hub for real-time push notifications. Pushes `NewOrder` (order notifications to admin dashboard bell), `NewMessage` (incoming WhatsApp messages to chat page), `MessageSent` (outgoing message confirmations). JWT-authenticated via query string token. |
| **Chat System** | `Controllers/ChatController.cs`, `Services/ChatService.cs`, `Models/ChatMessage.cs`, `DTOs/Chat/ChatDtos.cs`, `Data/Configurations/ChatMessageConfiguration.cs` | Full 2-way admin ↔ customer chat. Admin sends messages via dashboard → API → WhatsApp. Customer replies arrive via webhook → saved to DB → pushed to admin via SignalR. Bot auto-pauses when admin takes over, resumes after timeout. |
| **Background Processing** | `Services/BroadcastBackgroundService.cs` | Hosted `BackgroundService` + `Channel<T>` producer/consumer queue — `BroadcastService` enqueues jobs, `BroadcastBackgroundService` dequeues and processes with `SemaphoreSlim(10)` concurrency. Saves progress every 50 messages. Graceful shutdown via `CancellationToken`. |
| **Entity Configurations** | `Data/Configurations/ProductConfiguration.cs`, `CustomerConfiguration.cs`, `CartItemConfiguration.cs`, `OrderConfiguration.cs`, `OrderItemConfiguration.cs`, `BroadcastMessageConfiguration.cs`, `ChatMessageConfiguration.cs` | Fluent API: relationships (1:1, 1:N, M:1), indexes, unique constraints, delete behavior, seed data |
| **Split DTOs (validated)** | `DTOs/Product/`, `DTOs/Order/`, `DTOs/Customer/`, `DTOs/Dashboard/`, `DTOs/Broadcast/`, `DTOs/Payment/`, `DTOs/WhatsApp/`, `DTOs/Chat/` | Per-feature DTO files with `[Required]`, `[MaxLength]`, `[Range]`, `[Url]`, `[RegularExpression]` validation attributes |
| **DI Extensions** | `Extensions/ServiceCollectionExtensions.cs` | Grouped DI registration: `AddDatabase()`, `AddApplicationServices()`, `AddCorsPolicies()` |
| **Mapping Extensions** | `Extensions/MappingExtensions.cs` | `Product.ToDto()`, `Order.ToDto()`, `OrderItem.ToDto()` — shared entity-to-DTO mapping used by ProductService, OrderService, DashboardService |
| **Authentication** | `Controllers/AuthController.cs`, `Models/AdminUser.cs`, `DTOs/Auth/AuthDtos.cs`, `Data/Configurations/AdminUserConfiguration.cs` | JWT Bearer authentication — `POST /api/auth/login` validates credentials against `AdminUsers` table (BCrypt hash, case-sensitive). Returns JWT token (24h expiry). `[Authorize]` attribute on all admin controllers. Admin user auto-seeded on first startup. |
| **Config** | `appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json` | Environment-specific configuration files |
| **Data Models** | `Models/Product.cs`, `Customer.cs`, `CartItem.cs`, `Order.cs`, `OrderItem.cs`, `BroadcastMessage.cs`, `AdminUser.cs`, `ChatMessage.cs` | Entity classes with navigation properties |
| **Database** | `AppDbContext.cs` | EF Core DbContext — uses `ApplyConfigurationsFromAssembly()` for auto-discovering entity configs. 8 DbSets including AdminUsers and ChatMessages. |

### Frontend Admin Panel (Angular 18) — `LeatherShopAdmin/`

**Architecture:** Feature-based module structure with per-feature models, services, components, and route files. Lazy-loaded routes for each feature. Shared components in `shared/`.

| Feature Module | Route | Key Files |
|----------------|-------|-----------|
| **Dashboard** | `/dashboard` (lazy) | `features/dashboard/` — `dashboard.service.ts`, `dashboard.model.ts`, `dashboard.routes.ts`, `components/dashboard/` |
| **Products** | `/products` (lazy) | `features/products/` — `product.service.ts`, `product.model.ts`, `products.routes.ts`, `components/product-list/`, `components/product-form/` |
| **Orders** | `/orders` (lazy) | `features/orders/` — `order.service.ts`, `order.model.ts`, `orders.routes.ts`, `components/orders/` |
| **Customers** | `/customers` (lazy) | `features/customers/` — `customer.service.ts`, `customer.model.ts`, `customers.routes.ts`, `components/customers/` |
| **Broadcast** | `/broadcast` (lazy) | `features/broadcast/` — `broadcast.service.ts`, `broadcast.model.ts`, `broadcast.routes.ts`, `components/broadcast/` |
| **Chat** | `/chat` (lazy) | `features/chat/` — `chat.service.ts`, `chat.model.ts`, `chat.routes.ts`, `components/chat-page/` — WhatsApp-style 2-way chat with conversation sidebar, message history, bot pause/resume toggle |
| **Auth** | `/login` | `features/auth/components/login/` — animated login page with background video, JWT token storage, redirect to dashboard on success |
| **Core** | _(app-wide)_ | `core/interceptors/error.interceptor.ts` — HTTP error interceptor with toast notifications. `core/interceptors/auth.interceptor.ts` — attaches JWT Bearer token to all API requests. `core/guards/auth.guard.ts` — protects all admin routes (redirects to `/login` if no token). `core/services/auth.service.ts` — login, logout, token management, username extraction. `core/services/signalr.service.ts` — SignalR hub connection for real-time order notifications and chat messages. |
| **Shared** | _(all pages)_ | `shared/components/navbar/`, `shared/components/toast/`, `shared/components/loading-spinner/`, `shared/services/notification.service.ts`, `shared/services/template-loader.service.ts`, `shared/utils/severity.utils.ts` — Navbar includes notification bell with overlay panel for real-time order alerts (powered by SignalR) |
| **Environments** | _(build-time)_ | `environments/environment.ts` (dev), `environments/environment.prod.ts` (prod) — API URL + SignalR hub URL config |
| **App Shell** | — | `app.routes.ts` (lazy loading via `loadChildren`, `authGuard` on all admin routes, `**` wildcard → `/login`), `app.config.ts` (interceptors: auth + error), `app.component.ts` (toast + navbar + outlet, navbar hidden on login page) |

---

## How It Works — System Architecture

```
┌─────────────────────┐         ┌──────────────────────┐
│   CUSTOMER           │         │   SHOP OWNER          │
│   (WhatsApp)         │         │   (Browser)           │
└────────┬────────────┘         └──────────┬───────────┘
         │                                  │
         │ WhatsApp Messages                │ HTTPS
         ▼                                  ▼
┌─────────────────────┐         ┌──────────────────────┐
│  Meta WhatsApp       │         │  Angular 18           │
│  Cloud API           │         │  Admin Panel          │
│  (graph.facebook.com)│         │  (Vercel)             │
└────────┬────────────┘         └──────────┬───────────┘
         │                                  │
         │ Webhook POST                     │ REST API calls
         │ (Railway public URL)             │ (Railway API)
         ▼                                  ▼
┌──────────────────────────────────────────────────────┐
│              .NET 8 Web API                           │
│              (Railway - leathershop-production.up.railway.app) │
│                                                       │
│  ┌──────────────┐  ┌─────────────┐  ┌─────────────┐  │
│  │ Webhook       │  │ ChatBot     │  │ Admin       │  │
│  │ Controller    │──│ Service     │  │ Controllers │  │
│  └──────────────┘  └──────┬──────┘  └──────┬──────┘  │
│                           │          [Authorize]      │
│                           │          JWT Bearer       │
│                    ┌──────▼──────┐          │         │
│  ┌─────────────┐   │ WhatsApp    │◄─────────┘         │
│  │ Auth        │   │ Service     │ (order status      │
│  │ Controller  │   │ (sends msgs)│  notifications)    │
│  │ (JWT login) │   └─────────────┘                    │
│  └─────────────┘          │                           │
│                           │                           │
│  ┌─────────────┐   ┌──────▼──────────────────┐        │
│  │ SignalR Hub  │   │  PostgreSQL (EF Core)   │        │
│  │ (Notificatn)│   │  Products, Customers,   │        │
│  │ ─ NewOrder  │   │  CartItems, Orders,     │        │
│  │ ─ NewMessage│   │  OrderItems, Broadcasts,│        │
│  │ ─ MessageSnt│   │  ChatMessages, Admins   │        │
│  └──────┬──────┘   └────────────────────────┘         │
│         │ WebSocket          │                        │
│         ▼ (real-time)        │                        │
│  Admin Browser               │                        │
└──────────────────────────────────────────────────────┘
                           │
                    ┌──────▼──────┐
                    │  Razorpay   │
                    │  (Payment)  │
                    └─────────────┘
```

**How data flows:**

1. **Customer → WhatsApp → Meta API → Webhook → ChatBotService** — customer sends a message, Meta forwards it to your webhook endpoint, the chatbot processes it and responds
2. **ChatBotService → WhatsAppService → Meta API → Customer** — bot sends interactive menus, product details, cart summaries back to the customer
3. **Checkout → PaymentController → Razorpay** — bot sends a payment link, customer pays on a Razorpay-powered HTML page, payment verified and order confirmed
4. **Admin Panel → REST API → Database** — shop owner manages products, views orders, updates statuses
5. **Order Status Update → WhatsAppService → Customer** — when admin changes order status, customer gets an automatic WhatsApp notification
6. **New Order → SignalR → Admin Dashboard** — when a customer completes payment, `PaymentService` pushes a `NewOrder` notification via SignalR to the admin's navbar bell icon (real-time, no polling)
7. **Customer WhatsApp Message → Webhook → DB + SignalR → Admin Chat Page** — incoming customer messages are saved to `ChatMessages` table and pushed in real-time to the admin chat page via SignalR `NewMessage` event
8. **Admin Chat Reply → API → WhatsApp + DB + SignalR** — admin types a reply in the chat page, API sends it via WhatsApp and saves to DB, pushes `MessageSent` confirmation back via SignalR
9. **Bot Pause/Resume** — when admin sends a message to a customer, the chatbot auto-pauses for that customer (30 min default). Customer messages go to admin only, not the bot. Bot resumes automatically after timeout or when admin clicks "Resume Bot".

---

## Customer WhatsApp Flow

```
Customer sends "Hi" / "Hello" / "Menu"
    │
    ▼
┌─────────────────────────────────────────┐
│  MAIN MENU (Interactive List)           │
│  ┌─ Shop ──────────────────────────┐    │
│  │  🏷️ Browse Categories            │    │
│  │  🛒 View Cart                    │    │
│  │  💳 Checkout                     │    │
│  ├─ Account ───────────────────────┤    │
│  │  📦 My Orders                    │    │
│  └─────────────────────────────────┘    │
└─────────────────────────────────────────┘
         │
         ├── Browse Categories
         │       │
         │       ▼
         │   Category List (Wallets, Belts, Bags, Shoes, Accessories)
         │       │
         │       ▼
         │   Product List (up to 10 per category)
         │       │
         │       ▼
         │   Product Details (name, brand, price, stock, description)
         │       │
         │       ├── [🛒 Add to Cart] → "Added! Cart: X items"
         │       ├── [📙 Categories]  → back to categories
         │       └── [🏠 Main Menu]   → back to main menu
         │
         ├── View Cart
         │       │
         │       ▼
         │   Cart Summary (items, quantities, prices, total)
         │       │
         │       ├── [💳 Checkout]     → creates order + payment link
         │       ├── [🗑️ Clear Cart]   → empties cart
         │       └── [🛍️ Continue]     → back to browsing
         │
         ├── Checkout
         │       │
         │       ▼
         │   Order Created → Stock Reduced → Cart Cleared
         │       │
         │       ▼
         │   Payment Link sent (Razorpay HTML page)
         │       │
         │       ▼
         │   Customer Pays → Payment Verified → Order Confirmed
         │       │
         │       ▼
         │   WhatsApp: "✅ Payment Received! Order confirmed!"
         │
         └── My Orders
                 │
                 ▼
             Last 5 orders with: order number, amount, status, paid, date
```

**Message Types Used:**
- **Interactive List** — for menus with 4+ options (categories, products)
- **Interactive Buttons** — for 2-3 quick actions (Add to Cart / View Cart / Menu)
- **Image Messages** — for product details with photo + caption (requires uploaded image + Railway volume)
- **Plain Text** — for order details, confirmations, error messages
- **Template Messages** — for broadcasts (must be pre-approved by Meta)

---

## Admin Panel Flow

```
Admin opens http://localhost:4200
    │
    ▼
┌── LOGIN (/login) ──────────────────────────────────────┐
│  Animated background video + frosted glass card         │
│  Username: [________]  Password: [________]             │
│  [Sign In]                                              │
│  → On success: stores JWT token, redirects to dashboard │
│  → On failure: inline error message                     │
└────────────────────────────────────────────────────────┘
    │
    ▼ (authenticated — all routes protected by AuthGuard)
┌── DASHBOARD (/dashboard) ──────────────────────────────┐
│  [Products: 6] [Customers: 0] [Orders: 0] [Revenue: 0]│
│  [Pending: 0]  [Low Stock: 0]                          │
│  ┌─ Recent Orders Table ─────────────────────────────┐ │
│  │ Order#  │ Customer │ Amount │ Status │ Paid │ Date │ │
│  └───────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────┘

┌── PRODUCTS (/products) ────────────────────────────────┐
│  [Category Filter ▼] [Brand Filter ▼] [Search...]      │
│  [+ Add Product]                                        │
│  ┌─ Product List ────────────────────────────────────┐ │
│  │ Name │ Category │ Brand │ Price │ Stock │ Actions  │ │
│  │                          [Edit] [Delete]          │ │
│  └───────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────┘

┌── PRODUCT FORM (/products/new or /products/edit/:id) ──┐
│  Name, Description, Brand, Category, Price, Stock, URL  │
│  [Save]                                                  │
└──────────────────────────────────────────────────────────┘

┌── ORDERS (/orders) ────────────────────────────────────┐
│  [Status Filter ▼]                                      │
│  ┌─ Order List ──────────────────────────────────────┐ │
│  │ Order# │ Customer │ Phone │ Amount │ Items │       │ │
│  │ Status: [Pending ▼] → updates & sends WhatsApp    │ │
│  └───────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────┘

┌── CUSTOMERS (/customers) ──────────────────────────────┐
│  [Show Subscribers Only ☐]                              │
│  ┌─ Customer List ───────────────────────────────────┐ │
│  │ Phone │ Name │ Address │ Subscribed │ Orders │ Date│ │
│  └───────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────┘

┌── BROADCAST (/broadcast) ──────────────────────────────┐
│  Template Name: [________]  Language: [en]              │
│  Parameters: [________]     Image URL: [________]       │
│  [Send to All Subscribers]                              │
│  ┌─ Broadcast History ──────────────────────────────┐  │
│  │ Template │ Recipients │ Sent │ Failed │ Date      │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘

┌── CHAT (/chat) ────────────────────────────────────────┐
│  ┌─ Conversations ─┐  ┌─ Message Thread ─────────────┐ │
│  │ [Search...]      │  │ Customer: Hello              │ │
│  │ 🟢 Zaheer (2)   │  │ Bot: Welcome! Here is menu.. │ │
│  │   Last: Hi there│  │ Admin: Hi, how can I help?   │ │
│  │ ⚪ Syed          │  │ Customer: I want to order    │ │
│  │   Last: Thanks  │  │ [Type a message...] [Send]   │ │
│  └──────────────────┘  │                              │ │
│  Features:             │ [🤖 Pause Bot] [▶ Resume]    │ │
│  • Real-time messages  └──────────────────────────────┘ │
│  • Bot pause/resume per customer                        │
│  • Unread count badges                                  │
│  • WhatsApp-style chat bubbles                          │
└─────────────────────────────────────────────────────────┘

🔔 NOTIFICATION BELL (navbar, all pages)
   • Bell icon in top-right with red badge count
   • Overlay panel shows recent order notifications
   • Real-time via SignalR WebSocket (no polling)
   • Click notification → navigates to Orders page
```

---

## Project Structure

```
LeatherShopAPI/                          # ── .NET 8 Web API ──
│
├── Program.cs                           # App entry point — clean, uses extension methods
│                                        #   - Loads appsettings.Local.json (gitignored secrets)
│                                        #   - JWT Bearer authentication configuration
│                                        #   - SignalR configured with JWT auth via query string
│                                        #   - Uses ExceptionHandlingMiddleware
│                                        #   - Auto-runs EF migrations on startup
│                                        #   - Seeds admin user (reads password from config)
│                                        #   - Enables Swagger in development
│                                        #   - Maps SignalR hub at /hubs/notifications
│
├── appsettings.json                     # Config structure with empty placeholders (safe to commit)
├── appsettings.Local.json               # ⚠️ GITIGNORED — actual secrets for local development
├── appsettings.Local.json.example       # Template: copy to appsettings.Local.json and fill values
├── appsettings.Development.json         # Development overrides (log levels)
├── appsettings.Production.json          # Production overrides (log levels only, secrets via env vars)
│
├── Extensions/
│   ├── ServiceCollectionExtensions.cs   # Grouped DI registration
│   └── MappingExtensions.cs             # Entity → DTO extension methods
│                                        #   Product.ToDto(), Order.ToDto(), OrderItem.ToDto()
│                                        #   Eliminates duplicate mapping across services
│                                        #   - AddDatabase() — PostgreSQL context
│                                        #   - AddApplicationServices() — all 9 services
│                                        #   - AddCorsPolicies() — CORS for Angular (AllowCredentials for SignalR)
│
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs   # Global exception handler
│                                        #   - Catches all unhandled exceptions
│                                        #   - Maps to proper HTTP status codes
│                                        #   - Returns consistent ApiResponse JSON
│                                        #   - Prevents stack trace leaks
│
├── Models/
│   ├── ApiResponse.cs                   # Unified API response envelope
│   │                                    #   - ApiResponse<T> (with data)
│   │                                    #   - ApiResponse (without data)
│   │                                    #   - Static Ok() and Fail() factory methods
│   ├── Product.cs                       # Id, Name, Description, Brand, Category,
│   │                                    #   Price, StockQuantity, ImageUrl, IsActive
│   ├── Customer.cs                      # Id, PhoneNumber (unique), Name, Address,
│   │                                    #   IsSubscribed, IsBotPaused, BotPausedUntil
│   │                                    #   → has Orders, CartItems
│   ├── CartItem.cs                      # Id, CustomerId, ProductId, Quantity
│   │                                    #   (unique constraint: customer + product)
│   ├── Order.cs                         # Id, OrderNumber (unique), CustomerId,
│   │                                    #   TotalAmount, Status, PaymentId, IsPaid
│   │                                    # OrderItem: OrderId, ProductId, Qty, UnitPrice
│   ├── BroadcastMessage.cs              # Id, MessageTemplate, MessageBody,
│   │                                    #   TotalRecipients, SentCount, FailedCount
│   ├── ChatMessage.cs                   # Id, CustomerId, Direction (Incoming/Outgoing),
│   │                                    #   MessageType, Content, SenderName, IsFromBot,
│   │                                    #   Timestamp — stores all WhatsApp chat history
│   └── AdminUser.cs                     # Id, Username (unique), PasswordHash (BCrypt),
│                                        #   CreatedAt, LastLoginAt
│
├── Hubs/
│   └── NotificationHub.cs               # SignalR hub for real-time notifications
│                                        #   - NewOrder: pushed when customer completes payment
│                                        #   - NewMessage: pushed when customer sends WhatsApp msg
│                                        #   - MessageSent: pushed when admin message is delivered
│
├── Controllers/                         # THIN — wraps responses in ApiResponse<T>
│   ├── AuthController.cs                # JWT login — POST /api/auth/login
│   │                                    #   Validates credentials vs AdminUsers table
│   │                                    #   BCrypt password verification (case-sensitive)
│   │                                    #   Returns JWT token (24h expiry)
│   ├── ProductsController.cs            # [Authorize] — Injects IProductService
│   ├── OrdersController.cs              # [Authorize] — Injects IOrderService
│   ├── CustomersController.cs           # [Authorize] — Injects ICustomerService
│   ├── DashboardController.cs           # [Authorize] — Injects IDashboardService
│   ├── BroadcastController.cs           # [Authorize] — Injects IBroadcastService
│   ├── ChatController.cs                # [Authorize] — Injects IChatService
│   │                                    #   GET conversations, GET messages, POST send,
│   │                                    #   POST pause bot, POST resume bot
│   ├── PaymentController.cs             # Public (customer-facing) — Injects IPaymentService
│   └── WhatsAppWebhookController.cs     # Public (Meta webhook) — Injects IChatBotService
│                                        #   Saves incoming messages to ChatMessages table,
│                                        #   checks bot pause before routing to chatbot,
│                                        #   pushes NewMessage to admin via SignalR
│
├── Services/
│   ├── Interfaces/                      # Service contracts
│   │   ├── IWhatsAppService.cs          # SendText, SendList, SendButton, SendTemplate
│   │   ├── IChatBotService.cs           # ProcessMessage()
│   │   ├── IProductService.cs           # CRUD + categories/brands
│   │   ├── IOrderService.cs             # List + status update
│   │   ├── ICustomerService.cs          # List + create + import + subscribe
│   │   ├── IDashboardService.cs         # GetDashboard()
│   │   ├── IBroadcastService.cs         # Send + history + templates
│   │   ├── IChatService.cs              # Conversations, messages, send, bot pause/resume
│   │   └── IPaymentService.cs           # Payment page + verify
│   │
│   ├── WhatsAppService.cs               # Implements IWhatsAppService
│   ├── ChatBotService.cs                # Implements IChatBotService (state machine)
│   │                                    #   BotSend* wrappers save all outgoing messages
│   │                                    #   to ChatMessages + push via SignalR
│   ├── ProductService.cs                # Implements IProductService
│   ├── OrderService.cs                  # Implements IOrderService
│   ├── CustomerService.cs               # Implements ICustomerService
│   ├── DashboardService.cs              # Implements IDashboardService
│   ├── BroadcastService.cs              # Implements IBroadcastService (enqueues to Channel)
│   ├── BroadcastBackgroundService.cs    # Hosted BackgroundService — reads from Channel<T>,
│   │                                    #   processes broadcasts with SemaphoreSlim(10)
│   │                                    #   concurrency, saves progress every 50 messages
│   ├── ChatService.cs                   # Implements IChatService — conversations list,
│   │                                    #   paginated messages, send message via WhatsApp,
│   │                                    #   bot pause/resume with auto-expiry
│   └── PaymentService.cs                # Implements IPaymentService
│                                        #   Pushes NewOrder via SignalR on successful payment
│                                        #   Sends WhatsApp notification to shop owner
│
├── Data/
│   ├── AppDbContext.cs                  # 8 DbSets, uses ApplyConfigurationsFromAssembly()
│   └── Configurations/                  # Fluent API entity configurations
│       ├── ProductConfiguration.cs      # Indexes on Category/Brand, seed data
│       ├── CustomerConfiguration.cs     # Unique PhoneNumber, 1:N → Orders, 1:N → CartItems
│       ├── CartItemConfiguration.cs     # Unique (CustomerId+ProductId), M:1 relationships
│       ├── OrderConfiguration.cs        # Unique OrderNumber, M:1 → Customer, 1:N → OrderItems
│       ├── OrderItemConfiguration.cs    # M:1 → Order, M:1 → Product (Restrict delete)
│       ├── BroadcastMessageConfiguration.cs
│       ├── ChatMessageConfiguration.cs  # CustomerId+Timestamp composite index,
│       │                                    #   Direction stored as string, FK cascade delete
│       └── AdminUserConfiguration.cs    # Unique Username, max lengths
│
├── DTOs/                                # Split per feature, with validation attributes
│   ├── Auth/AuthDtos.cs                 # LoginRequest (Username, Password), LoginResponse (Token, Expiry, Username)
│   ├── Product/ProductDtos.cs           # [Required], [MaxLength], [Range], [Url]
│   ├── Order/OrderDtos.cs               # OrderDto, OrderItemDto
│   ├── Customer/CustomerDtos.cs         # [Required], [RegularExpression] phone, [MinLength]
│   ├── Dashboard/DashboardDtos.cs       # DashboardDto
│   ├── Broadcast/BroadcastDtos.cs       # [Required] template, [Url] image
│   ├── Chat/ChatDtos.cs                 # ConversationDto, ChatMessageDto, SendMessageDto,
│   │                                    #   BotPauseDto — DTOs for 2-way chat feature
│   ├── Payment/PaymentDtos.cs           # [Required] paymentId, orderId
│   └── WhatsApp/WhatsAppDtos.cs         # WhatsApp webhook payload classes
│
└── Migrations/                          # EF Core auto-generated migrations


LeatherShopAdmin/                        # ── Angular 18 Admin Panel ──
│
├── src/
│   ├── main.ts                          # Angular bootstrap
│   ├── index.html                       # Root HTML
│   ├── styles.scss                      # Global styles
│   │
│   ├── environments/
│   │   ├── environment.ts               # Dev config (apiUrl: localhost:8080, hubUrl for SignalR)
│   │   └── environment.prod.ts          # Prod config (apiUrl: production URL, hubUrl for SignalR)
│   │
│   └── app/
│       ├── app.component.ts             # Root: toast + navbar + router-outlet
│       ├── app.component.html
│       ├── app.config.ts                # provideRouter, provideHttpClient(withInterceptors)
│       ├── app.routes.ts                # Lazy-loaded routes via loadChildren()
│       │
│       ├── core/
│       │   ├── guards/
│       │   │   ├── auth.guard.ts        # CanActivateFn — checks localStorage token,
│       │   │   │                        #   redirects to /login if missing
│       │   │   └── unsaved-changes.guard.ts  # CanDeactivateFn — prompt on dirty form
│       │   ├── interceptors/
│       │   │   ├── auth.interceptor.ts  # Attaches JWT Bearer token to all API requests
│       │   │   └── error.interceptor.ts # HTTP error interceptor — catches all API
│       │   │                            #   errors, shows toast notifications
│       │   │                            #   Skips toast for login 401 (handled inline)
│       │   │                            #   Auto-redirects to /login on 401 (expired token)
│       │   └── services/
│       │       ├── auth.service.ts      # login(), logout(), isLoggedIn(), getUsername()
│       │       │                        #   JWT token management via localStorage
│       │       └── signalr.service.ts   # SignalR hub connection manager
│       │                                #   Connects to /hubs/notifications with JWT auth
│       │                                #   Exposes newOrder$ and newMessage$ observables
│       │                                #   Auto-reconnects on disconnect
│       │
│       ├── shared/
│       │   ├── utils/
│       │   │   └── severity.utils.ts         # Shared getStatusSeverity() + getStatusButtonSeverity()
│       │   │                                 #   Used by dashboard + orders components
│       │   ├── services/
│       │   │   ├── notification.service.ts    # Centralized toast notification service
│       │   │   └── template-loader.service.ts # Shared WhatsApp template loading + validation
│       │   │                                  #   Used by broadcast + customers components
│       │   └── components/
│       │       ├── navbar/              # Navigation bar (ts, html, scss)
│       │       │                        #   Includes notification bell with badge count
│       │       │                        #   OverlayPanel shows real-time order alerts
│       │       │                        #   Powered by SignalR (starts on init, stops on logout)
│       │       ├── toast/               # Toast notification component (auto-dismiss)
│       │       └── loading-spinner/     # Reusable loading spinner component
│       │
│       └── features/                    # Feature-based modules
│           ├── auth/
│           │   └── components/login/    # Animated login page with background video
│           │                            #   JWT token stored on success, redirects to dashboard
│           │
│           ├── dashboard/
│           │   ├── models/dashboard.model.ts
│           │   ├── services/dashboard.service.ts  # Uses environment.apiUrl
│           │   ├── dashboard.routes.ts
│           │   └── components/dashboard/    (ts, html, scss)
│           │
│           ├── products/
│           │   ├── models/product.model.ts
│           │   ├── services/product.service.ts    # Uses environment.apiUrl
│           │   ├── products.routes.ts
│           │   ├── components/product-list/  (ts, html, scss)
│           │   └── components/product-form/  (ts, html, scss)
│           │
│           ├── orders/
│           │   ├── models/order.model.ts
│           │   ├── services/order.service.ts      # Uses environment.apiUrl
│           │   ├── orders.routes.ts
│           │   └── components/orders/        (ts, html, scss)
│           │
│           ├── customers/
│           │   ├── models/customer.model.ts
│           │   ├── services/customer.service.ts   # Uses environment.apiUrl
│           │   ├── customers.routes.ts
│           │   └── components/customers/     (ts, html, scss)
│           │
│           └── broadcast/
│               ├── models/broadcast.model.ts
│               ├── services/broadcast.service.ts  # Uses environment.apiUrl
│               ├── broadcast.routes.ts
│               └── components/broadcast/     (ts, html, scss)
│
│           └── chat/
│               ├── models/chat.model.ts           # Conversation, ChatMessage, SendMessage interfaces
│               ├── services/chat.service.ts       # REST API client for chat endpoints
│               ├── chat.routes.ts                 # Lazy-loaded /chat route
│               └── components/chat-page/     (ts, html, scss)
│                                                  #   WhatsApp-style 2-panel layout:
│                                                  #   Left: conversation sidebar with search + unread badges
│                                                  #   Right: message thread with chat bubbles + send input
│                                                  #   Bot pause/resume toggle per customer
│                                                  #   Real-time updates via SignalR
```

---

## Developer Setup Guide

### Repository Access

This repo is **private**. Only the owner and added collaborators can clone/push.

```bash
git clone https://github.com/mohamedzaheer236-beep/LeatherShop.git
cd LeatherShop
```

### Current Project Reference Values

> **For developers and AI assistants:** This section lists all known identifiers, account details, and non-secret reference values for this project. **Actual secret tokens/passwords are NOT stored here** — they live in `appsettings.Local.json` (gitignored) or Railway environment variables.

#### Admin Panel Login

| Field | Value | Notes |
|-------|-------|-------|
| **Username** | `Admin` | Case-sensitive (capital A). Auto-seeded on first startup. Stored in `AdminUsers` PostgreSQL table. |
| **Password** | _(your `Admin:SeedPassword` value)_ | Set in `appsettings.Local.json` → `Admin.SeedPassword`. BCrypt-hashed in DB. |
| **Login URL** | `http://localhost:4200` (dev) / Vercel URL (prod) | Redirects to `/login` automatically |
| **Auth type** | JWT Bearer token, 24h expiry | Stored in `localStorage`. Attached to API calls by `auth.interceptor.ts`. |

#### URLs

| Service | Local Development | Production |
|---------|-------------------|------------|
| **API** | `http://localhost:8080` | `https://leathershop-production.up.railway.app` |
| **Swagger** | `http://localhost:8080/swagger` | `https://leathershop-production.up.railway.app/swagger` |
| **Admin Panel** | `http://localhost:4200` | `https://leather-shop-liard.vercel.app` |
| **SignalR Hub** | `http://localhost:8080/hubs/notifications` | `https://leathershop-production.up.railway.app/hubs/notifications` |
| **WhatsApp Webhook** | `https://YOUR_NGROK_URL/api/whatsapp/webhook` | `https://leathershop-production.up.railway.app/api/whatsapp/webhook` |

#### WhatsApp Business Account

| Field | Value | Notes |
|-------|-------|-------|
| **Business Portfolio** | Leather Shop (ID: `1270862431810807`) | Meta Business Settings |
| **WABA ID** | `2151682048973965` | WhatsApp Business Account |
| **Phone Number ID** | `1055485577637232` | Used in `WhatsApp:PhoneNumberId` config |
| **Phone Number** | +91 79043 03876 | The bot's WhatsApp number customers message |
| **System User** | Leathershop (Admin type) | Permanent token holder |
| **API Version** | `v22.0` | Set in `appsettings.json` → `WhatsApp:ApiVersion` |
| **Webhook Verify Token** | _(your `WhatsApp:VerifyToken` value)_ | Must match Meta Console webhook config |
| **Owner Phone** | `YOUR_PHONE_NUMBER` | Receives order notifications via WhatsApp |

#### WhatsApp Message Templates

| Template Name | Type | Template ID | Status |
|---------------|------|-------------|--------|
| `shop_deals` | MARKETING | `2107912596695779` | Pending Meta approval |
| `order_update` | UTILITY | `1636258954059739` | Pending Meta approval |
| `store_notification` | UTILITY | `2317291185767700` | Pending Meta approval |
| `hello_world` | — | _(Meta default)_ | Approved (test numbers only) |

#### Database

| Field | Value |
|-------|-------|
| **Engine** | PostgreSQL 14+ |
| **Database Name** | `LeatherShopDB` (auto-created by EF Core migrations) |
| **Default Username** | `postgres` |
| **ORM** | Entity Framework Core 8 |
| **Tables** | Products, Customers, CartItems, Orders, OrderItems, BroadcastMessages, ChatMessages, AdminUsers |
| **Seed Data** | 6 leather products + 1 admin user auto-seeded on first run |

#### GitHub Repository

| Field | Value |
|-------|-------|
| **Repo URL** | `https://github.com/mohamedzaheer236-beep/LeatherShop.git` |
| **Visibility** | Private |
| **Branch** | `main` |

---

### Prerequisites — Install These First

| # | Software | Version | Purpose | Download |
|---|----------|---------|---------|----------|
| 1 | **PostgreSQL** | 14+ | Database | [postgresql.org/download](https://www.postgresql.org/download/) |
| 2 | **pgAdmin 4** | Latest | Database GUI (optional but recommended) | Comes with PostgreSQL installer |
| 3 | **.NET 8 SDK** | 8.0+ | Backend runtime | [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0) |
| 4 | **Node.js** | 18+ | Angular CLI & frontend build | [nodejs.org](https://nodejs.org/) |
| 5 | **Angular CLI** | 18+ | Frontend dev server | `npm install -g @angular/cli` |
| 6 | **Git** | Latest | Version control | [git-scm.com](https://git-scm.com/) |

### Step 1: PostgreSQL Database

1. Install PostgreSQL and make sure it's running on `localhost:5432`
2. Remember the password you set for the `postgres` user during installation
3. The database `LeatherShopDB` will be **created automatically** by EF Core migrations when the API starts — you do NOT need to create it manually

### Step 2: Configure Local Secrets

> **Important:** `appsettings.json` contains only empty placeholders and is safe to commit. All secrets go in `appsettings.Local.json` which is **gitignored**.

#### Configuration Hierarchy (highest priority wins)

| # | Source | When Used | Committed to Git? |
|---|--------|-----------|--------------------|
| 1 | **Environment variables** | Railway production (`Jwt__Key`, `WhatsApp__AccessToken`, etc.) | No |
| 2 | **`appsettings.Local.json`** | Local development (auto-loaded by `Program.cs`) | No (gitignored) |
| 3 | **`dotnet user-secrets`** | Alternative for local dev (stored in `%APPDATA%`) | No |
| 4 | **`appsettings.{Environment}.json`** | Environment-specific non-secret overrides | Yes (no secrets) |
| 5 | **`appsettings.json`** | Base config structure — empty placeholders only | Yes (no secrets) |

#### Complete Secrets Reference

| Config Key | Required? | What It Is | Where To Get It | What Happens If Missing |
|------------|-----------|-----------|-----------------|-------------------------|
| `ConnectionStrings:DefaultConnection` | **YES** | PostgreSQL connection string | Set during PostgreSQL installation. Format: `Host=localhost;Port=5432;Database=LeatherShopDB;Username=postgres;Password=YOUR_PG_PASSWORD` | API crashes on startup — cannot connect to database |
| `Jwt:Key` | **YES** | Secret key for signing JWT tokens. Must be **≥32 characters**. | Generate any random string ≥32 chars (e.g., `openssl rand -base64 48`). Each environment should use a **different** key. | API crashes on startup with `InvalidOperationException` |
| `Admin:SeedPassword` | **First run only** | Password for the auto-created `Admin` user. Used only when the `AdminUsers` table is empty. | Choose any secure password. This becomes the login password at `http://localhost:4200`. | API crashes on first run. Safe to leave empty after first admin is seeded. |
| `WhatsApp:PhoneNumberId` | No* | Meta WhatsApp phone number ID | Meta Developer Console → WhatsApp → API Setup → Phone Number ID | WhatsApp chatbot won't send/receive messages |
| `WhatsApp:BusinessAccountId` | No* | Meta WhatsApp Business Account ID | Meta Developer Console → WhatsApp → API Setup → Business Account ID | WhatsApp features won't work |
| `WhatsApp:AccessToken` | No* | Meta API access token (permanent System User token for production) | Meta Business Settings → System Users → Generate Token (see [WhatsApp Setup](#whatsapp-business-api-setup)) | WhatsApp features won't work |
| `WhatsApp:VerifyToken` | No* | Webhook verification string — must match what you enter in Meta Console | Choose any custom string (e.g., `my_verify_token_2026`) | Meta webhook verification fails |
| `Razorpay:KeyId` | No* | Razorpay API key (test mode: `rzp_test_...`, live: `rzp_live_...`) | [razorpay.com](https://razorpay.com/) → Dashboard → Settings → API Keys | Payment page won't load |
| `Razorpay:KeySecret` | No* | Razorpay API secret | Same as above — shown once when key is generated | Payment signature verification skipped (insecure) |
| `App:OwnerPhone` | No* | Shop owner's WhatsApp number with country code, no `+` (e.g., `YOUR_PHONE_NUMBER`) | Your phone number in international format without `+` | Owner won't receive order notification WhatsApp messages |

> \* These are only needed for WhatsApp chatbot and payment features. The **admin panel, products, orders, customers, and dashboard** all work without them.

#### Quick Setup

Copy the example file and fill in your values:

```bash
cd LeatherShopAPI
copy appsettings.Local.json.example appsettings.Local.json   # Windows
# cp appsettings.Local.json.example appsettings.Local.json   # macOS/Linux
```

Then edit `LeatherShopAPI/appsettings.Local.json` with your actual credentials:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=LeatherShopDB;Username=postgres;Password=YOUR_POSTGRES_PASSWORD"
  },
  "Jwt": {
    "Key": "YOUR_JWT_SECRET_KEY_MINIMUM_32_CHARACTERS_LONG"
  },
  "WhatsApp": {
    "PhoneNumberId": "YOUR_WHATSAPP_PHONE_NUMBER_ID",
    "BusinessAccountId": "YOUR_WHATSAPP_BUSINESS_ACCOUNT_ID",
    "AccessToken": "YOUR_WHATSAPP_ACCESS_TOKEN",
    "VerifyToken": "YOUR_WEBHOOK_VERIFY_TOKEN"
  },
  "Razorpay": {
    "KeyId": "rzp_test_xxxxx",
    "KeySecret": "YOUR_RAZORPAY_SECRET"
  },
  "App": {
    "OwnerPhone": "YOUR_PHONE_WITH_COUNTRY_CODE_NO_PLUS"
  },
  "Admin": {
    "SeedPassword": "YOUR_SECURE_ADMIN_PASSWORD"
  }
}
```

> **Note:** You can start with just `ConnectionStrings`, `Jwt:Key`, and `Admin:SeedPassword` configured. WhatsApp and Razorpay can be set up later. The admin panel and API will work without them — only the chatbot and payments need those keys.

**Alternative: dotnet user-secrets** (advanced)
```bash
cd LeatherShopAPI
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=LeatherShopDB;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "Jwt:Key" "YOUR_JWT_SECRET_KEY_MINIMUM_32_CHARACTERS_LONG"
dotnet user-secrets set "Admin:SeedPassword" "YOUR_ADMIN_PASSWORD"
```

### Step 3: Run the Backend API

```bash
cd LeatherShopAPI
dotnet run
```

- API starts on **http://localhost:8080** (default; set `$env:PORT=5000` for port 5000)
- Swagger UI at **http://localhost:8080/swagger**
- On first run, it auto-creates the database and seeds 6 sample leather products

### Step 4: Run the Angular Admin Panel

```bash
cd LeatherShopAdmin
npm install        # first time only
npx ng serve
```

- Admin panel opens at **http://localhost:4200**
- It calls the API at `http://localhost:8080` (configured in `src/environments/environment.ts`)

### Step 5: Verify Everything Works

1. Open **http://localhost:8080/swagger** — you should see all API endpoints
2. Open **http://localhost:4200** — you should be redirected to the login page
3. Log in with:
   - **Username:** `Admin` (capital A — case-sensitive)
   - **Password:** whatever you set as `Admin:SeedPassword` in `appsettings.Local.json`
4. Dashboard should load with 6 products, 0 orders
5. Go to **Products** page — you should see the 6 seeded products

---

## External Services Setup

These are only needed when you want the WhatsApp chatbot and payments to work. The admin panel works without them.

### WhatsApp Business API Setup

#### 1. Create Meta Developer App
1. Go to [Meta for Developers](https://developers.facebook.com/)
2. Create a new app → Select **"Business"** type
3. Add **"WhatsApp"** product to your app

#### 2. Get API Credentials
1. WhatsApp section → **API Setup**
2. Copy **Phone Number ID** → paste in `appsettings.json` → `WhatsApp:PhoneNumberId`
3. Copy **Temporary Access Token** → paste in `WhatsApp:AccessToken` (for local development)
4. For production: generate a **Permanent Token** via System Users in Business Settings (see below)

#### 3. Permanent WhatsApp Access Token (Production)

Temporary tokens expire every 24 hours. For production, use a **permanent System User token**:

1. Go to [Meta Business Settings](https://business.facebook.com/settings/) → **Users** → **System Users**
2. Create a new **Admin** System User (e.g., "Leathershop")
3. Click **Generate New Token** → select your WhatsApp app
4. Grant permissions: `whatsapp_business_management`, `whatsapp_business_messaging`
5. Token type: **Permanent** (never expires)
6. Copy the token → set as `WhatsApp__AccessToken` environment variable on Railway

> **Note:** This replaces the need for temporary tokens and ngrok for local development. The production API on Railway receives webhooks directly.

#### 4. Configure Webhook in Meta Console

**For Production (Railway):**
1. Meta Developer Console → WhatsApp → Configuration → **Webhook**
2. **Callback URL**: `https://leathershop-production.up.railway.app/api/whatsapp/webhook`
3. **Verify Token**: same value as `WhatsApp:VerifyToken` environment variable
4. Subscribe to: **`messages`**

**For Local Development (ngrok):**
1. Install ngrok: `choco install ngrok` (Windows) or download from [ngrok.com](https://ngrok.com/download)
2. Authenticate: `ngrok config add-authtoken YOUR_NGROK_AUTH_TOKEN`
3. Start tunnel: `ngrok http 5000`
4. Update webhook URL in Meta Console to `https://YOUR_NGROK_URL/api/whatsapp/webhook`
5. Update `App:BaseUrl` in `appsettings.json` with the ngrok URL

> **Note:** ngrok is only needed for local development. In production, Railway provides a permanent public URL.

#### 5. Test the Chatbot
1. In Meta Console → WhatsApp → API Setup → find your **test phone number**
2. Send **"Hi"** to that number from your personal WhatsApp
3. You should receive the interactive main menu

#### 6. WhatsApp Green Tick (Production Only)
1. Complete Business Verification in Meta Business Settings
2. Enable two-factor authentication
3. Submit official business details (name, address, website)
4. Apply for Official Business Account in WhatsApp Manager
5. Requires a legitimate business with online presence

### Razorpay Payment Setup

1. Create account at [razorpay.com](https://razorpay.com/)
2. Get **Key ID** and **Key Secret** from Dashboard → Settings → API Keys
3. For testing, use **Test Mode** keys (prefix `rzp_test_`)
4. Paste into `appsettings.json` → `Razorpay:KeyId` and `Razorpay:KeySecret`

> **Note:** The payment verification in the current code does NOT validate the Razorpay signature (marked as TODO). For production, you must implement HMAC SHA256 signature verification.

---

## API Endpoints Reference

### Authentication
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/login` | Login with username + password. Returns JWT token (24h expiry). |

> All endpoints below (except Payment and WhatsApp Webhook) require `Authorization: Bearer <token>` header.

### Products
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/products` | List all products. Query params: `?category=Wallet&brand=Royal Leather&search=classic` |
| GET | `/api/products/{id}` | Get single product by ID |
| POST | `/api/products` | Create product (JSON body: name, description, brand, category, price, stockQuantity, imageUrl) |
| PUT | `/api/products/{id}` | Update product (partial update — send only fields to change) |
| DELETE | `/api/products/{id}` | Delete product |
| GET | `/api/products/categories` | List distinct active product categories |
| GET | `/api/products/brands` | List distinct active product brands |

### Orders
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/orders` | List all orders. Query params: `?status=Pending` |
| PUT | `/api/orders/{id}/status` | Update status (JSON body: `"Confirmed"`). Sends WhatsApp notification. |

### Customers
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/customers` | List all customers. Query params: `?subscribedOnly=true&search=phone_or_name` |
| GET | `/api/customers/count` | Get subscriber count and total count |
| POST | `/api/customers` | Create a single customer (sends WhatsApp welcome message) |
| POST | `/api/customers/import` | Bulk import customers from list |
| PUT | `/api/customers/{id}` | Update customer name, address, subscription. **No WhatsApp message sent on edit.** |
| DELETE | `/api/customers/{id}` | Delete customer + cascade delete all orders, cart, chat messages |
| PUT | `/api/customers/{id}/subscribe` | Toggle subscription status |

### Dashboard
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/dashboard` | Dashboard stats + 10 recent orders |

### Broadcast
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/broadcast/send` | Send template message to all subscribers |
| GET | `/api/broadcast/history` | Last 20 broadcast records |

### Chat (2-Way Admin ↔ Customer)
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/chat/conversations` | List all conversations (customers with chat history). Query: `?search=name` |
| GET | `/api/chat/{customerId}/messages` | Paginated message history. Query: `?page=1&pageSize=50` |
| POST | `/api/chat/{customerId}/send` | Send message to customer via WhatsApp. Body: `{ message }`. Auto-pauses bot 30min. |
| POST | `/api/chat/{customerId}/toggle-bot` | Toggle chatbot pause/resume for a customer |
| DELETE | `/api/chat/{customerId}/messages` | Delete all chat messages for a customer conversation |

### SignalR Hub
| Hub URL | Event | Payload | Description |
|---------|-------|---------|-------------|
| `/hubs/notifications` | `NewOrder` | `{ orderNumber, customerName, amount, timestamp }` | Pushed when customer completes payment |
| `/hubs/notifications` | `NewMessage` | `{ customerId, customerName, content, timestamp, ... }` | Pushed when customer sends a WhatsApp message |
| `/hubs/notifications` | `MessageSent` | `{ customerId, content, timestamp, ... }` | Pushed when admin/bot message is delivered |

> SignalR hub requires JWT authentication via `?access_token=<token>` query string.

### WhatsApp Webhook
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/whatsapp/webhook` | Meta webhook URL verification |
| POST | `/api/whatsapp/webhook` | Receive incoming WhatsApp messages |

### Payment
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/payment/pay/{orderId}` | Serve Razorpay payment HTML page |
| POST | `/api/payment/verify` | Verify payment and confirm order |

---

## Database Schema

```
┌─────────────┐       ┌──────────────┐       ┌──────────────┐
│  Products    │       │  Customers   │       │  BroadcastMsg│
├─────────────┤       ├──────────────┤       ├──────────────┤
│ Id (PK)     │       │ Id (PK)      │       │ Id (PK)      │
│ Name        │       │ PhoneNumber ◄│─unique │ MsgTemplate  │
│ Description │       │ Name         │       │ MessageBody  │
│ Brand    ◄──│─index │ Address      │       │ TotalRecip   │
│ Category ◄──│─index │ IsSubscribed │       │ SentCount    │
│ Price       │       │ IsBotPaused  │       │ FailedCount  │
│ StockQty    │       │ BotPausedUntl│       │ SentAt       │
│ ImageUrl    │       │ CreatedAt    │       └──────────────┘
│ IsActive    │       │ UpdatedAt    │
│ CreatedAt   │       └──────┬───────┘
│ UpdatedAt   │              │
└──────┬──────┘              │ 1:N               ┌──────────────┐
       │              ┌──────▼───────┐           │  AdminUsers  │
       │              │  CartItems   │           ├──────────────┤
       │              ├──────────────┤           │ Id (PK)      │
       │   ┌──────────│ Id (PK)      │           │ Username  ◄──│─unique
       │   │          │ CustomerId(FK)│          │ PasswordHash │
       │   │          │ ProductId(FK)│◄─unique   │ CreatedAt    │
       │   │          │ Quantity     │(Cust+Prod)│ LastLoginAt  │
       │   │          │ AddedAt      │           └──────────────┘
       │   │          └──────────────┘
       │   │
       │   │          ┌──────────────┐       ┌──────────────┐
       │   │          │  Orders      │       │  OrderItems  │
       │   │          ├──────────────┤       ├──────────────┤
       │   │          │ Id (PK)      │──1:N──│ Id (PK)      │
       │   │          │ OrderNumber ◄│unique  │ OrderId (FK) │
       │   └─────────►│ CustomerId(FK│       │ ProductId(FK)│
       │              │ TotalAmount  │       │ Quantity     │
       └─────────────►│ Status (enum)│       │ UnitPrice    │
                      │ PaymentId    │       └──────────────┘
                      │ IsPaid       │
                      │ ShippingAddr │       ┌──────────────┐
                      │ CreatedAt    │       │ ChatMessages │
                      │ UpdatedAt    │       ├──────────────┤
                      └──────────────┘       │ Id (PK)      │
                                             │ CustomerId(FK│◄─cascade
                                             │ Direction    │ (Incoming/Outgoing)
                                             │ MessageType  │ (text/interactive/image)
                                             │ Content      │
                                             │ SenderName   │
                                             │ IsFromBot    │ (true=bot, false=admin)
                                             │ Timestamp ◄──│─composite index
                                             └──────────────┘  (CustomerId+Timestamp)

Order Status Enum: Pending → Confirmed → Shipped → Delivered → Cancelled
```

**Seed Data (auto-inserted on first run):**

| # | Product | Brand | Category | Price |
|---|---------|-------|----------|-------|
| 1 | Classic Leather Wallet | Royal Leather | Wallet | ₹899 |
| 2 | Executive Leather Belt | Royal Leather | Belt | ₹1,299 |
| 3 | Leather Messenger Bag | Heritage Craft | Bag | ₹3,499 |
| 4 | Leather Oxford Shoes | StepCraft | Shoes | ₹4,999 |
| 5 | Leather Keychain | Royal Leather | Accessories | ₹299 |
| 6 | Leather Laptop Sleeve | Heritage Craft | Bag | ₹2,499 |

---

## What Is NOT Yet Implemented

These features are not built yet and would need to be added for production:

| Feature | Details |
|---------|---------|
| ~~**Authentication / Authorization**~~ | ✅ **IMPLEMENTED** — JWT Bearer auth with BCrypt password hashing. Admin credentials stored in PostgreSQL `AdminUsers` table. Auto-seeded on first startup. `[Authorize]` on all admin controllers. Angular auth guard + interceptor + animated login page. |
| ~~**Image Upload**~~ | ✅ **IMPLEMENTED** — Server-side file upload endpoint (`POST /api/products/upload-image`). Images saved to `wwwroot/uploads/` with GUID filenames. Type validation (JPG/PNG/WebP/GIF) and 5 MB size limit on both client and server. Served via `app.UseStaticFiles()`. Frontend: drag-to-browse dropzone with live preview and remove button. |
| ~~**Razorpay Signature Verification**~~ | ✅ **IMPLEMENTED** — `PaymentService.VerifyPaymentAsync` now computes HMAC-SHA256 from `RazorpayOrderId|PaymentId` using `Razorpay:KeySecret` and compares to client signature. Rejects mismatched signatures. |
| **Logging to File/Service** | Uses default console logging only. Need Serilog or similar for production. |
| **Rate Limiting** | No API rate limiting on admin endpoints. |
| ~~**Pagination**~~ | ✅ **IMPLEMENTED** — Orders have server-side pagination with `PaginatedResult<T>` (`GET /api/orders?page=1&pageSize=25`). Frontend uses PrimeNG `p-paginator` (25/50/100 rows). Customer table uses client-side pagination (all records loaded for checkbox selection). DB indexes on `IsSubscribed`, `CreatedAt`, `Status`, `IsPaid`, `IsActive`. |
| ~~**Product Image in WhatsApp**~~ | ✅ **IMPLEMENTED** — `SendImageMessage` added to `IWhatsAppService`/`WhatsAppService` (WhatsApp Cloud API `image` type with `link` + `caption`). `ChatBotService.SendProductDetails()` sends product photo with all details as the caption when `ImageUrl` is set. Constructs full public URL from `RAILWAY_PUBLIC_DOMAIN` env var (auto-provided by Railway) with `App:BaseUrl` config as primary source. Falls back gracefully to text-only button message if image send fails (try-catch with `LogWarning`). Caption and body text truncated to WhatsApp's 1024-char limit. Action buttons (Add to Cart / Categories / Menu) sent as a separate follow-up message since WhatsApp image messages don't support inline interactive buttons. **Requires:** Railway Volume mounted at `/app/wwwroot/uploads` for image persistence across redeployments. |
| ~~**Customer Address Collection**~~ | ✅ **IMPLEMENTED** — Bot asks for shipping address at checkout if not set. If address exists, shows Confirm/Change buttons before placing order. Address stored on `Customer.Address` and copied to `Order.ShippingAddress`. Admin UI requires address on create/edit (min 10 chars). |
| **Order Cancellation by Customer** | No WhatsApp flow for customers to cancel orders. |
| ~~**HTTPS in Production**~~ | ✅ **DEPLOYED** — Railway provides HTTPS automatically via Metal Edge. API accessible at `https://leathershop-production.up.railway.app`. |
| ~~**Permanent WhatsApp Access Token**~~ | ✅ **IMPLEMENTED** — Admin System User "Leathershop" created under "Leather Shop" Business Portfolio with permanent token (never expires). WABA ID: 2151682048973965, Phone Number ID: 1055485577637232, Phone: +91 79043 03876. Deployed to Railway as `WhatsApp__AccessToken` environment variable. |
| ~~**WhatsApp Message Templates**~~ | ✅ **CREATED** — 3 templates created: `shop_deals` (MARKETING, ID: 2107912596695779), `order_update` (UTILITY, ID: 1636258954059739), `store_notification` (UTILITY, ID: 2317291185767700). All PENDING Meta approval. `hello_world` approved but restricted to test phone numbers only. |
| ~~**Production Deployment**~~ | ✅ **DEPLOYED** — Backend API on **Railway** (`leathershop-production.up.railway.app`), PostgreSQL on **Railway** (managed instance with persistent volume), Frontend on **Vercel** (static Angular build). WhatsApp webhook URL updated to Railway. All environment variables configured via Railway dashboard. See [Deployment Guide](#deployment-guide) below. |

---

## Code Audit Report

A comprehensive audit of the entire codebase. Findings organized by severity.

### 🔴 CRITICAL — Must Fix Before Any Deployment

| # | Issue | Location | Details |
|---|-------|----------|---------|
| C1 | ~~**No Authentication / Authorization**~~ | ~~All controllers, `Program.cs`~~ | **FIXED** — JWT Bearer authentication implemented. `AuthController` with BCrypt password verification against `AdminUsers` table. `[Authorize]` attribute on all admin controllers (Products, Orders, Customers, Dashboard, Broadcast). Payment and WhatsApp webhook remain public. Angular: `AuthGuard` protects all admin routes, `AuthInterceptor` attaches Bearer token, animated login page, auto-redirect on 401. Admin credentials auto-seeded on first DB migration. |
| C2 | ~~**Secrets Committed to Source**~~ | ~~`appsettings.json`~~ | **FIXED** — All secrets (DB password, JWT key, WhatsApp access token, admin seed password) moved out of `appsettings.json` into `appsettings.Local.json` (gitignored). Base `appsettings.json` now contains only empty placeholders and non-secret config. `Program.cs` loads `appsettings.Local.json` at startup (optional, never committed). Admin seed password read from `Admin:SeedPassword` config instead of hardcoded. `.csproj` has `UserSecretsId` for developers preferring `dotnet user-secrets`. Production secrets come from Railway environment variables. `appsettings.Local.json.example` template committed for new developers. |
| C3 | ~~**Razorpay Signature Verification TODO'd Out**~~ | ~~`PaymentService.cs`~~ | **FIXED** — `VerifyPaymentAsync` now computes HMAC-SHA256 signature from `RazorpayOrderId|PaymentId` using the `Razorpay:KeySecret` config value. When `KeySecret` is configured, verification is **mandatory** — missing signature or mismatch rejects the payment. When `KeySecret` is not configured (dev mode), logs a warning and allows the payment. `PaymentVerifyDto` updated with `RazorpayOrderId` field. Payment page JS passes `razorpay_order_id` in the verify request. |
| C4 | **WhatsApp Webhook Signature Not Validated** | `WhatsAppWebhookController.cs` | Meta sends `X-Hub-Signature-256` on every POST. The controller never checks it. Attackers can POST fabricated payloads to trigger chatbot flows and create fake orders. |
| C5 | ~~**XSS in Payment Page**~~ | ~~`PaymentController.cs`~~ | **FIXED** — All user-controlled values (`OrderNumber`, `CustomerPhone`, `ProductName`) are HTML-encoded with `WebUtility.HtmlEncode()` into safe local variables before interpolation into the payment HTML page. Numeric values (`TotalAmount`, `Quantity`, etc.) are strongly-typed decimals/ints and don't need encoding. |
| | C6 | ~~**DbContext Thread-Safety Bug**~~ | ~~`BroadcastBackgroundService.cs`~~ | **FIXED** — `ProcessBroadcastAsync` no longer shares a single `DbContext` across concurrent tasks. Each concurrent task creates its own `IServiceScope` (at most 10 alive at once via `SemaphoreSlim`). `SaveProgressAsync` uses a dedicated scope with `ExecuteUpdateAsync` (stateless SQL `UPDATE`, no entity tracking). The initial broadcast existence check uses a short-lived scope that is disposed before concurrency begins. No `DbContext` instance is ever accessed from multiple threads. | |

### 🟠 HIGH — Data Integrity / Bugs

| # | Issue | Location | Details |
|---|-------|----------|---------|
| H1 | **Race Condition: Overselling During Checkout** | `ChatBotService.cs` | Stock checked with `if (product.StockQuantity < qty)` then decremented in same method. Two concurrent checkouts can both pass and oversell. Fix: use optimistic concurrency (`RowVersion`) or `UPDATE WHERE StockQuantity >= @qty`. |
| H2 | ~~**Phone Format Mismatch → Duplicate Customers**~~ | ~~`CustomerService.cs` vs `ChatBotService.cs`~~ | **FIXED** — Created `PhoneNumberHelper.Normalize()` static helper that strips `+`, spaces, dashes, parentheses. Applied to all phone number entry points: `ChatBotService.ProcessMessage()` (normalizes `from` before lookup/create), `CustomerService.CreateAsync()` (normalizes input), `CustomerService.BulkImportAsync()` (normalizes each phone), `BroadcastService.SendBroadcastAsync()` (normalizes DTO phone numbers). All phone numbers stored without `+` prefix (e.g., `919876543210`) matching WhatsApp API format. |
| H3 | ~~**No HTTPS Enforcement**~~ | `Program.cs`, `launchSettings.json` | **MITIGATED** — Railway provides HTTPS automatically. Local dev still uses HTTP. |
| H4 | ~~**Stock Not Restored on Order Cancellation**~~ | ~~`OrderService.cs`~~ | **FIXED** — `UpdateStatusAsync` now loads `OrderItems` with `Products` via `.Include()`. When status changes to `Cancelled` (and wasn’t already cancelled), restores `StockQuantity` for each order item. Prevents double-restore by checking previous status. |
| H5 | **Description MaxLength Mismatch** | `ProductConfiguration.cs` | **PARTIALLY FIXED** — `Product.cs` `[MaxLength]` aligned to 2000, but Fluent API config still says `.HasMaxLength(1000)`. **EF Core Fluent API takes precedence** — DB column still limited to 1000. See F48. |
| H6 | ~~**Production API URL is a Placeholder**~~ | `environment.prod.ts` | **FIXED** — Points to `https://leathershop-production.up.railway.app/api`. |
| H7 | ~~**No 404 Wildcard Route**~~ | ~~`app.routes.ts`~~ | **FIXED** — Added `{ path: '**', redirectTo: 'login' }` wildcard route. Invalid URLs now redirect to login page (which redirects to dashboard if already authenticated). |
| H8 | ~~**Duplicate Error Toasts**~~ | ~~`error.interceptor.ts`~~ | **FIXED** — Error interceptor now skips toast for login 401 responses (`req.url.includes('/auth/login')`) to prevent double notification (login component shows inline error). Generic 401 message changed to "Session expired. Please log in again." |

### 🟡 MEDIUM — Performance / Code Quality

| # | Issue | Location | Details |
|---|-------|----------|---------|
| M1 | ~~**No Pagination on Any List Endpoint**~~ | ~~All services, all controllers~~ | **FIXED** — Orders API returns server-side paginated results via `PaginatedResult<T>` (generic model with `Items`, `TotalCount`, `Page`, `PageSize`, `TotalPages`). `GET /api/orders` accepts `?page=1&pageSize=25` query params (clamped 1-100). Frontend orders page uses PrimeNG `p-paginator` with 25/50/100 rows, fetches only the current page from the API. Customer table uses client-side pagination (25/50/100 rows via PrimeNG `[paginator]`) — correct for the selection use-case where all customers must be in memory for checkbox state. DB indexes added for all filtered/sorted columns. |
| M2 | ~~**N+1 Queries in BulkImport**~~ | ~~`CustomerService.cs`~~ | **FIXED** — Replaced per-customer `AnyAsync` query with a single `SELECT PhoneNumber` query that loads all existing phone numbers into a `HashSet<string>`. Then checks containment in O(1) per import entry. Also prevents duplicates within the same import batch by adding to the HashSet as we go. 1000 imports = 1 DB query instead of 1000. |
| M3 | **`.ToLower()` in LINQ Kills DB Indexes** | `ProductService.cs`, `CustomerService.cs` | `p.Category.ToLower() == category.ToLower()` translates to `LOWER()` in SQL, preventing PostgreSQL from using indexes. Use `EF.Functions.ILike()` for case-insensitive search on Npgsql. |
| M4 | **No `OnPush` Change Detection** | All 7 Angular components | All use default change detection. Extra re-renders on every event. `OnPush` would significantly reduce CD cycles. |
| M5 | ~~**Memory Leaks: No Unsubscribe**~~ | All 6 feature components | **FIXED** — Product-list simplified to button-triggered search (no `valueChanges` subscriptions). All HTTP `subscribe()` calls auto-complete — no leak risk. Observable patterns are leak-safe by design. |
| M6 | ~~**Product Search on Every Keystroke**~~ | `product-list.component.html` | **FIXED** — Removed `(input)="onSearch()"`. API call now fires only via dedicated Search button (`pi pi-search`) or Enter key (`keyup.enter`). No debounce needed — user explicitly triggers search. |
| M7 | ~~**No `trackBy` on Any `*ngFor`**~~ | ~~All list templates~~ | **FIXED** — Orders list has `trackBy: trackByOrderId` on the main `*ngFor`. Prevents full DOM re-renders when order list is refreshed. Other lists either use `p-table` (handles DOM diffing internally) or have static collections. |
| M8 | **ChatBotService is a 520-Line God Class** | `ChatBotService.cs` | Cart logic, checkout, order history, menu routing all in one class. Should decompose into smaller handlers (CartHandler, CheckoutHandler, MenuHandler). |
| M9 | ~~**Dashboard Makes 7 Separate DB Roundtrips**~~ | ~~`DashboardService.cs`~~ | **ANALYZED — Sequential is correct.** EF Core's `DbContext` is NOT thread-safe — `Task.WhenAll` on the same context throws `InvalidOperationException`. The 7 queries are simple COUNTs that execute in <1ms each on PostgreSQL with indexes. Total ~7ms. Added `AsNoTracking()` to the recent orders query to skip change tracking overhead. |
| M10 | **No Rate Limiting** | All controllers | Broadcast endpoint can be abused to spam all customers. Webhook has no rate limiting. |
| M11 | ~~**Google Fonts via `@import url()` + PrimeNG Broken Font Files**~~ | ~~`styles.scss`, `angular.json`, `index.html`~~ | **FIXED** — Moved Google Fonts Inter from `@import url()` in SCSS to `<link>` in `index.html` with `preconnect` hints (faster, non-render-blocking). PrimeNG's lara-light-indigo theme ships with corrupted `Inter-roman.var.woff2` / `Inter-italic.var.woff2` that Angular's esbuild bundler can't serve correctly — caused 30+ "Failed to decode downloaded font" + "OTS parsing error" console errors. Fix: copied theme CSS to `public/primeng-theme.css` with broken `@font-face` declarations stripped, loaded as static `<link>` instead of bundled via `styles[]`. Override `--font-family: 'Inter', sans-serif` in `:root` so PrimeNG uses Google Fonts. |
| M12 | ~~**`getTotalSent()` Method Called in Template**~~ | ~~`broadcast.component.ts`~~ | **FIXED** — Replaced `getTotalSent()` getter method with a cached `totalSent` property that is computed once when broadcast history loads. Template now uses `{{ totalSent }}` instead of calling a method on every change detection cycle. Also added `OnDestroy` lifecycle hook with `pollingInterval` cleanup to prevent memory leaks from `setInterval`. |

### 🟢 LOW — Nice to Have / Best Practices

| # | Issue | Location | Details |
|---|-------|----------|---------|
| L1 | **No Health Check Endpoint** | `Program.cs` | No `/health` or `/ready` for load balancers / Kubernetes probes / uptime monitoring. |
| L2 | **No API Versioning** | All controllers | No `/api/v1/...` prefix. Breaking changes will affect all clients simultaneously. |
| L3 | **No ESLint / Prettier** | `package.json` | Zero static code analysis or formatting enforcement on the frontend. |
| L4 | **No Tests** | `angular.json` | `skipTests: true` everywhere. Zero test files in the entire project. |
| L5 | **Hardcoded Currency `₹`** | All templates with prices | Uses `&#8377;` directly. Should use Angular's `currency` pipe for i18n support. |
| L6 | ~~**60+ `!important` in Styles**~~ | `styles.scss` | **FIXED** — All 60+ `!important` removed. PrimeNG overrides now use `body .p-*` prefix for natural specificity. |
| L7 | ~~**No CSS Variables**~~ | `styles.scss` | **FIXED** — Added 75+ `--ls-*` CSS custom properties in `:root` (brand, accent, text, surface, border, radius, shadow, font tokens). Full theming support. |
| L8 | ~~**Code Duplication**~~ | Multiple files | **FIXED** — `getSeverity()` extracted to `shared/utils/severity.utils.ts` (used by dashboard + orders). Template loading extracted to `shared/services/template-loader.service.ts` (used by broadcast + customers). DTO mapping extracted to `Extensions/MappingExtensions.cs` with `Product.ToDto()`, `Order.ToDto()`, `OrderItem.ToDto()` (used by ProductService, OrderService, DashboardService). |
| L9 | ~~**Accessibility Gaps**~~ | Multiple templates | **FIXED** — Products `<p-tag>` (Active/Inactive toggle) has `role="button"`, `tabindex="0"`, `keydown.enter`/`keydown.space`, `aria-label`. Order expand `<div>` has `role="button"`, `tabindex="0"`, `aria-expanded`, keyboard handlers. Loading spinner has `role="status"` + `aria-live="polite"`. Skip-to-content link added to app shell with focus-visible styling. Customers tag kept click-only (dedicated `<p-button>` already provides keyboard access). |
| L10 | ~~**No Form Validation Messages**~~ | `product-form.component.html` | **FIXED** — Inline `<small class="p-error">` error messages on all 5 required fields (name, brand, category, price, stock). `ng-invalid`/`ng-dirty` classes applied for red border feedback. `submitted` flag prevents errors before first submit. Toast notification per specific validation failure. |
| L11 | ~~**No Unsaved Changes Guard**~~ | `product-form.component.ts` | **FIXED** — `CanDeactivateFn` guard (`unsaved-changes.guard.ts`) with `confirm()` dialog. `window:beforeunload` handler for browser tab close. JSON snapshot comparison for dirty detection. `savedSuccessfully` flag bypasses guard after save. Wired to `/products/new` and `/products/edit/:id` routes. |
| L12 | **UI State Mixed into Data Model** | `customer.model.ts` | `selected?: boolean` belongs in component state, not in the data model interface. |
| L13 | ~~**Unused `Router` Injections**~~ | `navbar.component.ts`, `customers.component.ts` | **FIXED** — Removed unused `Router` imports and constructor injections from both components. |
| L14 | ~~**Dead Code: `filteredCustomers`**~~ | `customers.component.ts` | **FIXED** — Removed unused `filteredCustomers` property and its assignment. |
| L15 | **No Active Route Highlighting** | `navbar.component.ts` | Navbar doesn't visually indicate which page the user is on. |
| L16 | **No Order Status Transition Validation** | `OrderService.cs` | No guard against invalid transitions (e.g., `Delivered` → `Pending`, `Cancelled` → `Shipped`). |
| L17 | **Hard Delete on Products** | `ProductService.cs` | `_db.Products.Remove()` with no soft delete. Products referenced by orders will crash (FK constraint). No audit trail. |
| L18 | **Auto-Migration at Startup** | `Program.cs` | `db.Database.Migrate()` runs synchronously. With multiple instances, concurrent migrations can deadlock. Should be a CI/CD step. |
| L19 | **WhatsApp Auth Header Set in Constructor** | `WhatsAppService.cs` | If the token is rotated in config, the service keeps the stale token until app restart. |
| L20 | **Helper Models Inside Service File** | `WhatsAppService.cs` | `ListSection`, `ListRow`, `ButtonOption`, `WhatsAppTemplate` defined at bottom of service file. Should be in `Models/WhatsApp/`. |
| L21 | **No `CancellationToken` Propagation** | All controllers/services | If a client disconnects, the server continues processing until completion. |
| L22 | **No `[ProducesResponseType]` Attributes** | All controllers | Swagger has no typed response documentation (200, 400, 404, etc.). |

### 🔧 Pending Fixes (Feb 24, 2026 — Full Audit)

Comprehensive line-by-line audit of the entire codebase. These remain to be fixed:

#### Backend — Security & Bugs

| # | Severity | Issue | File | Details |
|---|----------|-------|------|---------|
| P1 | **High** | Timing attack on HMAC comparison | `PaymentService.cs` L79 | `computedHash != dto.Signature` uses standard string `!=` which short-circuits on first mismatch. Attacker can brute-force the signature byte-by-byte. **Fix:** Use `CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(computedHash), Encoding.UTF8.GetBytes(dto.Signature))`. |
| P2 | **High** | Payment amount precision loss | `PaymentService.cs` L42 | `(int)(order.TotalAmount * 100)` truncates instead of rounding. ₹99.999 → 9999 paise instead of 10000. **Fix:** `(int)Math.Round(order.TotalAmount * 100)`. |
| P3 | **High** | `int.Parse` on user input | `ChatBotService.cs` L88, L96 | `int.Parse(input.Replace("prod_", ""))` throws `FormatException` on malformed input like `prod_abc`. Outer catch handles it but sends generic error. **Fix:** Use `int.TryParse`. |
| P4 | **Medium** | Swallowed exception | `OrderService.cs` L95 | Empty `catch { }` on WhatsApp notification silently discards all errors. **Fix:** `catch (Exception ex) { _logger.LogWarning(ex, "..."); }` — requires adding `ILogger<OrderService>`. |
| P5 | **Medium** | DivideByZero in pagination | `PaginatedResult.cs` L14 | `TotalCount / (double)PageSize` when `PageSize = 0` produces `Infinity` → undefined `(int)` cast. **Fix:** `PageSize <= 0 ? 0 : (int)Math.Ceiling(...)`. |
| P6 | **Medium** | Wrong `CreatedAtAction` target | `CustomersController.cs` L42 | `CreatedAtAction(nameof(GetAll), new { id = result.Id }, ...)` — `GetAll` doesn't accept an `id` parameter, producing an incorrect `Location` header. **Fix:** Replace with `Ok(...)`. |
| P7 | **Medium** | Misleading error UX | `PaymentController.cs` L80-82 | `.catch()` handler shows "Payment Received!" when verify API call fails. User thinks payment succeeded. **Fix:** Show "Payment may have been received. Please check your WhatsApp for confirmation." or retry. |
| P8 | **Medium** | No order status validation | `OrdersController.cs` L38 | `[FromBody] string newStatus` accepts any string. Invalid status values written to DB. Already partially fixed in service (Enum.TryParse) but controller should also validate. |
| P9 | **Low** | AuthController inconsistency | `AuthController.cs` | Uses manual `new ApiResponse<object> { ... }` instead of `ApiResponse.Fail()` / `ApiResponse<T>.Ok()` factory methods used by all other controllers. Also uses `[Microsoft.AspNetCore.Authorization.Authorize]` fully-qualified instead of `using` + `[Authorize]`. |

#### Backend — Code Quality

| # | Severity | Issue | File | Details |
|---|----------|-------|------|---------|
| P10 | **Medium** | No order status transition validation | `OrderService.cs` L64 | Allows invalid transitions (Delivered→Pending, Cancelled→Shipped). Should enforce a state machine. |
| P11 | **Medium** | `.ToLower()` kills DB indexes | `CustomerService.cs` L35, `ProductService.cs` | `p.Category.ToLower()` generates `LOWER()` SQL preventing index usage. **Fix:** Use `EF.Functions.ILike()` for PostgreSQL. |
| P12 | **Medium** | Null-forgiving config access | `WhatsAppService.cs` L22-24 | `_config["WhatsApp:PhoneNumberId"]!` throws `NullReferenceException` if config missing. **Fix:** Validate in constructor with `ArgumentException`. |
| P13 | **Low** | No `AsNoTracking()` on read queries | `OrderService.cs` L24-28, `CustomerService.cs` L27, `ProductService.cs` L20 | Read-only queries track entities needlessly. Add `.AsNoTracking()`. |
| P14 | **Low** | Shared HttpClient header mutation | `WhatsAppService.cs` L37 | Sets `DefaultRequestHeaders.Authorization` in constructor. If `HttpClient` is shared, this is not thread-safe. **Fix:** Set per-request or configure in `AddHttpClient<>`. |
| P15 | **Low** | `Information`-level payload logging | `WhatsAppService.cs` L188 | Logs full WhatsApp request JSON (contains phone numbers) at `Information` level. Should be `Debug`. |

#### Frontend — Quality

| # | Severity | Issue | File | Details |
|---|----------|-------|------|---------|
| P16 | **Medium** | Clickable `p-tag` missing a11y | `customers.component.html` L137-140 | Subscribe/Unsubscribe `<p-tag>` used as toggle button but missing `role="button"`, `tabindex="0"`, and keyboard handlers. A `<p-button>` in the Actions column already provides keyboard access, so not blocking. |
| P17 | **Medium** | PrimeNG internal API access | `product-list.component.ts` L58-63 | `clearDropdownFilter` accesses private PrimeNG properties (`filterValue`, `onFilterInputChange`). May break on PrimeNG upgrades. |
| P18 | **Low** | `any` types | `product-list.component.ts`, `customers.component.ts`, `orders.component.ts`, `broadcast.service.ts` | ~6 instances of `any` type across components. Should use proper types (`Dropdown`, `HttpErrorResponse`, `PaginatorState`). |
| P19 | **Low** | `::ng-deep` usage | `toast.component.ts` L11-57 | Deprecated in Angular. Still functional but may be removed. Consider `ViewEncapsulation.None` or global styles. |

### ✅ What's Already Good (Organization-Level Strengths)

| # | Strength |
|---|----------|
| 1 | All 5 features properly **lazy-loaded** with `loadChildren` |
| 2 | Clean **service → controller** separation with interfaces in the backend |
| 3 | Global **exception handling middleware** that prevents stack trace leaks |
| 4 | **Unified API response** envelope (`ApiResponse<T>`) across all endpoints |
| 5 | **Standalone components** throughout (Angular 18 best practice, no NgModules) |
| 6 | PrimeNG overrides in global styles. One scoped `::ng-deep` in product-list for dropdown panel (required by `appendTo="body"`) |
| 7 | **Channel\<T\> + BackgroundService** pattern for async broadcast processing |
| 8 | **EF Core Fluent API configurations** properly separated with index definitions |
| 9 | **Strict TypeScript** config with all Angular strict compiler flags enabled |
| 10 | Clean **feature-based folder structure** — features, shared, core properly organized |
| 11 | **HTTP error interceptor** provides consistent user-facing error messages |
| 12 | **Environment files** for dev/prod configuration switching |
| 13 | **Input validation** via DataAnnotations on all DTOs |
| 14 | **Split DTO pattern** — separate files per feature with validation attributes |
| 15 | **DI extension methods** for clean startup configuration |
| 16 | **Responsive UI** with PrimeNG + polished global styles |
| 17 | **JWT authentication** with BCrypt password hashing, DB-stored credentials, auto-seeded admin user |
| 18 | **Animated login page** with background video, inline error messages, and smooth transitions |
| 19 | **Route guards** (`AuthGuard`) protecting all admin routes with auto-redirect to login |
| 20 | **Auth interceptor** attaching Bearer token + error interceptor with smart 401 handling (login vs expired) |
| 21 | **Dynamic categories** in product form — fetched from API instead of hardcoded |
| 22 | **Error handlers** on all `subscribe()` calls with user-facing notifications and state rollback |\n| 23 | **SignalR real-time** WebSocket hub for order notifications + chat messages — no polling, instant push to all connected admins |\n| 24 | **2-way WhatsApp chat** with persistent message history, conversation sidebar, chat bubbles, unread badges |\n| 25 | **Bot pause/resume** system — chatbot auto-pauses when admin takes over a conversation, resumes after timeout |\n| 26 | **Component file consistency** — all substantial components use separate `.html` + `.scss` files (templateUrl/styleUrl pattern) |
| 27 | **Auto-cleanup background service** — `ChatCleanupBackgroundService` deletes chat messages older than 30 days (runs daily, uses bulk `ExecuteDeleteAsync`) |
| 28 | **Full customer CRUD** — create, edit, delete with cascade deletes. Edit does NOT send WhatsApp messages (intentional). |
| 29 | **Address mandatory workflow** — Admin UI requires address field on create/edit. Bot asks for shipping address during checkout if not set (`PendingAction` state machine). |
| 30 | **Confirmation dialogs** — Delete customer and delete conversation both use confirmation dialogs to prevent accidental data loss |

---

## Deployment Guide

The application is **fully deployed** and running 24/7 in production. The API must always be online for WhatsApp webhooks — Meta sends webhook events whenever a customer messages, and if the API is offline, those messages are lost after retry expiry.

### Production Architecture (Current)

```
┌──────────────────────┐     ┌──────────────────────────────────┐     ┌─────────────────────┐
│  Angular SPA         │     │  .NET 8 Web API                  │     │  PostgreSQL DB      │
│  (Static Build)      │     │  (Always Running)                │     │  (Managed)          │
│                      │     │                                  │     │                     │
│  Vercel              │────▶│  Railway                         │────▶│  Railway Postgres   │
│  leather-shop.vercel │     │  leathershop-production.up.      │     │  (postgres-volume)  │
│  .app                │     │  railway.app                     │     │                     │
└──────────────────────┘     └──────────────────────────────────┘     └─────────────────────┘
                                        │
                                 Meta WhatsApp Cloud API
                                 (webhook → Railway URL)
```

### Live URLs

| Component | URL | Platform |
|-----------|-----|----------|
| **Backend API** | `https://leathershop-production.up.railway.app` | Railway |
| **Swagger UI** | `https://leathershop-production.up.railway.app/swagger` | Railway (also used as health check) |
| **Admin Panel** | `https://leather-shop-liard.vercel.app` | Vercel |
| **WhatsApp Webhook** | `https://leathershop-production.up.railway.app/api/whatsapp/webhook` | Railway |

### What Was Deployed (Step-by-Step)

#### 1. Database — Railway PostgreSQL

- Created a **PostgreSQL** service on Railway (same project as the API)
- Railway provides `DATABASE_URL` environment variable automatically
- Persistent storage via **postgres-volume**
- EF Core auto-migrates on startup (`context.Database.Migrate()` in `Program.cs`)
- The API's `AddDatabase()` extension method parses Railway's `DATABASE_URL` URI format into Npgsql connection string automatically:
  ```csharp
  // Railway provides DATABASE_URL in URI format — convert to Npgsql format
  var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
  if (!string.IsNullOrEmpty(databaseUrl))
  {
      var uri = new Uri(databaseUrl);
      var userInfo = uri.UserInfo.Split(':');
      connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};..."
  }
  ```

#### 2. Backend API — Railway (.NET 8)

Deployed from GitHub with auto-builds on push.

**Dockerfile** (`LeatherShopAPI/Dockerfile`) — Multi-stage build:
```dockerfile
# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY LeatherShopAPI/LeatherShopAPI.csproj LeatherShopAPI/
RUN dotnet restore LeatherShopAPI/LeatherShopAPI.csproj
COPY LeatherShopAPI/ LeatherShopAPI/
RUN dotnet publish LeatherShopAPI/LeatherShopAPI.csproj -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "LeatherShopAPI.dll"]
```

**Railway Configuration** (`railway.toml`):
```toml
[build]
dockerfilePath = "LeatherShopAPI/Dockerfile"
watchPatterns = ["LeatherShopAPI/**"]

[deploy]
healthcheckPath = "/swagger/index.html"
healthcheckTimeout = 300
restartPolicyType = "ON_FAILURE"
restartPolicyMaxRetries = 10
```

**Environment Variables** (set in Railway dashboard):
| Variable | Description |
|----------|-------------|
| `DATABASE_URL` | Auto-set by Railway PostgreSQL service |
| `Jwt__Key` | Strong random secret key (min 32 chars) for JWT signing |
| `Jwt__Issuer` | `LeatherShopAPI` |
| `Jwt__Audience` | `LeatherShopAdmin` |
| `WhatsApp__PhoneNumberId` | Meta phone number ID (`YOUR_PHONE_NUMBER_ID`) |
| `WhatsApp__BusinessAccountId` | Meta business account ID |
| `WhatsApp__AccessToken` | **Permanent** System User token (never expires) |
| `WhatsApp__VerifyToken` | Webhook verification token |
| `Razorpay__KeyId` | Razorpay API key |
| `Razorpay__KeySecret` | Razorpay API secret |
| `App__BaseUrl` | `https://leathershop-production.up.railway.app` (used for payment links; WhatsApp images use `RAILWAY_PUBLIC_DOMAIN` as fallback) |
| `App__OwnerPhone` | Shop owner's WhatsApp number with country code, no `+` (e.g., `YOUR_PHONE_NUMBER`) — receives order notifications via WhatsApp |
| `Admin__SeedPassword` | Admin user seed password (only used on first startup when `AdminUsers` table is empty) |
| `FRONTEND_URL` | Vercel frontend URL (for CORS) |
| `RAILWAY_PUBLIC_DOMAIN` | Auto-provided by Railway (e.g., `leathershop-production.up.railway.app`) — used as fallback for constructing public image URLs when `App__BaseUrl` is not configured |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `PORT` | Auto-set by Railway |

**Key deployment changes made:**
- `Program.cs` — reads `PORT` env variable for Railway, Swagger enabled in all environments (used as health check)
- `ServiceCollectionExtensions.cs` — `AddDatabase()` parses Railway `DATABASE_URL` URI format, `AddCorsPolicies()` reads `FRONTEND_URL` env var for production CORS
- `appsettings.Production.json` — only log-level overrides (no secrets), actual secrets set via Railway environment variables listed above

#### 3. Frontend — Vercel (Angular Static Site)

**Option A: Vercel (Free, recommended for Angular)**

1. Go to [vercel.com](https://vercel.com/) → Sign up with GitHub
2. Click **"Import Project"** → select the `LeatherShop` repository
3. Set **Root Directory** to `LeatherShopAdmin`
4. **Framework**: Angular
5. **Build Command**: `ng build --configuration production`
6. **Output Directory**: `dist/leather-shop-admin/browser`
7. Click **Deploy** → deployed at `https://leather-shop-liard.vercel.app`

**Production API URL** is already configured in `environment.prod.ts`:
```typescript
export const environment = {
  production: true,
  apiUrl: 'https://leathershop-production.up.railway.app/api'
};
```

Vercel auto-deploys on every push to the `main` branch. Angular SPA routing is handled automatically by Vercel's framework detection.

#### 4. WhatsApp Webhook — Updated to Railway

1. Meta Developer Console → WhatsApp → Configuration → **Webhook**
2. **Callback URL**: `https://leathershop-production.up.railway.app/api/whatsapp/webhook`
3. **Verify Token**: same value as `WhatsApp__VerifyToken` environment variable on Railway
4. Subscribed to: **`messages`**
5. Using **permanent System User access token** (replaces temporary tokens that expired every 24h)

> **Note:** ngrok is no longer needed for production. Railway provides a permanent public URL that Meta can reach 24/7.

#### 5. Post-Deployment Checklist

- [x] WhatsApp webhook URL updated to Railway production API URL
- [x] Permanent WhatsApp access token configured (System User, never expires)
- [x] All environment variables set on Railway (DB, WhatsApp, Razorpay, JWT)
- [x] CORS updated — `FRONTEND_URL` env var for production Angular URL
- [x] HTTPS working (Railway provides it automatically via Metal Edge)
- [x] Database migration runs automatically on first startup (`context.Database.Migrate()` in `Program.cs`)
- [x] Health check configured (`/swagger/index.html` with 300s timeout)
- [x] Auto-restart on failure (max 10 retries)
- [x] Frontend deployed to Vercel with auto-deploy from GitHub
- [x] Railway Volume `leathershop-volume` mounted at `/app/wwwroot/uploads` (persists product images across deploys)
- [ ] Test WhatsApp message flow end-to-end (after Meta template approval)
- [ ] Test payment flow with Razorpay production keys
- [ ] Monitor logs via Railway dashboard

### Estimated Cost

| Component | Provider | Cost |
|-----------|----------|------|
| Angular SPA | Vercel | **Free** |
| .NET 8 API | Railway | **$5/month** (Trial: 30 days + $5.00 credit) |
| PostgreSQL | Railway | Included in Railway plan |
| Domain (optional) | Namecheap / GoDaddy | ~$10/year |
| **Total** | | **~$5/month** |

### Deployment Troubleshooting

#### Vercel Not Auto-Deploying from GitHub Pushes

**Problem:** Pushes to `main` don't trigger Vercel deployments. The Deployments page shows only old builds. Deploy hooks also return PENDING but nothing appears.

**Root Cause:** The Vercel GitHub App integration can silently break — the webhook stops firing. This happened after initial setup despite the Git connection appearing healthy in Settings → Git.

**Fix (Vercel CLI deploy — fastest):**

```powershell
# 1. Install and login to Vercel CLI
cd "d:\New folder\LeatherShopAdmin"
npx vercel login
# → Opens browser for OAuth login

# 2. Link to existing project (run from REPO ROOT, not LeatherShopAdmin — 
#    because Vercel's Root Directory setting is already "LeatherShopAdmin")
cd "d:\New folder"
npx vercel link --yes --project leather-shop

# 3. Deploy to production
npx vercel --prod
# → ✅ Production: https://leather-shop-liard.vercel.app
```

**Important notes:**
- Run `vercel link` and `vercel --prod` from the **repo root** (`d:\New folder`), NOT from `LeatherShopAdmin/` — because Vercel's Root Directory is already set to `LeatherShopAdmin`. Running from inside it causes a doubled path error.
- The **Git author email** must match the Vercel account email (`mohamedzaheer236@gmail.com`). If it's set to something else (e.g., `admin@leathershop.com`), Vercel CLI will reject with "Git author must have access to the team". Fix with: `git config user.email "mohamedzaheer236@gmail.com"`
- If the last commit used a wrong author, amend it: `git commit --amend --author="mohamedzaheer236 <mohamedzaheer236@gmail.com>" --no-edit --allow-empty`

**Alternative Fix (reconnect Git):**
1. Vercel → Settings → Git → **Disconnect** the repo
2. Re-add `mohamedzaheer236-beep/LeatherShop` repository
3. This recreates the GitHub webhook and should restore auto-deploy

**Deploy Hook (manual trigger):**
- A deploy hook was created: Settings → Git → Deploy Hooks → `redeploy` / `main`
- Trigger via POST: `Invoke-RestMethod -Uri "<hook_url>" -Method POST`
- Note: This only works if the Git integration is healthy. If webhooks are broken, the hook may return PENDING but never build.

#### Railway API Login Fails Right After Redeployment

**Problem:** Login returns "Login failed" on the Vercel frontend immediately after a Railway redeployment.

**Root Cause:** Railway containers have a cold-start period after deployment. The first request may timeout or fail while the .NET app is starting up (DB migration, JWT setup, etc.).

**Fix:** Wait 30-60 seconds after Railway shows "Deployment successful", then try again. Check Railway deployment logs → "View logs" to confirm the app says `Now listening on: http://[::]:8080`.

#### Local Development — API Starts on Wrong Port

**Problem:** API starts on port 8080 instead of 5000 locally.

**Root Cause:** `Program.cs` reads the `PORT` environment variable (for Railway). Default is 8080.

**Fix:** Set the environment variable before running:
```powershell
$env:PORT = "5000"
cd "d:\New folder\LeatherShopAPI"
dotnet run
# → Now listening on http://[::]:5000
```

#### Git Author Email Configuration

**Current configuration:**
- **GitHub account**: `mohamedzaheer236@gmail.com` (username: `mohamedzaheer236-beep`)
- **Git config** (must match for Vercel CLI): `git config user.email "mohamedzaheer236@gmail.com"`
- **Previously was**: `admin@leathershop.com` — this caused Vercel CLI to reject deployments

To check current email: `git config user.email`
To fix: `git config user.email "mohamedzaheer236@gmail.com"`

---

### Recently Implemented (Enterprise Patterns)

| Feature | Status |
|---------|--------|
| **Input Validation** | ✅ DataAnnotations (`[Required]`, `[MaxLength]`, `[Range]`, `[Url]`, `[RegularExpression]`) on all DTOs |
| **Error Handling Middleware** | ✅ `ExceptionHandlingMiddleware` — catches all exceptions, maps to HTTP codes, returns `ApiResponse` JSON |
| **Unified API Response** | ✅ `ApiResponse<T>` envelope with `Success`, `Message`, `Data`, `Errors` on all endpoints |
| **Environment-based Config** | ✅ `appsettings.Production.json` + Angular `environment.ts` / `environment.prod.ts` with `fileReplacements` |
| **HTTP Error Interceptor** | ✅ Angular functional `HttpInterceptorFn` — catches HTTP errors, shows user-friendly toast notifications |
| **Toast Notification System** | ✅ `NotificationService` + `ToastComponent` — centralized pub/sub toast with auto-dismiss |
| **Loading Spinner** | ✅ Reusable `LoadingSpinnerComponent` integrated into feature components |
| **DI Extension Methods** | ✅ `ServiceCollectionExtensions` — grouped registration (`AddDatabase`, `AddApplicationServices`, `AddCorsPolicies`) |
| **Broadcast Background Processing** | ✅ Replaced fire-and-forget `Task.Run` with proper `BackgroundService` + `Channel<T>` producer/consumer queue. `BroadcastService` enqueues a `BroadcastJob` → `BroadcastBackgroundService` (hosted) dequeues and processes one broadcast at a time. Uses `SemaphoreSlim(10)` for controlled concurrency (10 parallel sends). Saves progress every 50 messages. Supports graceful shutdown via `CancellationToken`. |
| **Multi-quantity in Cart** | ✅ Chatbot asks "How many?" via `PendingProductId` state → customer types a number → validates against stock (including existing cart quantity) → adds with chosen quantity |
| **Immediate Navigation** | ✅ Removed `setTimeout(() => navigate, 1500)` from product form — now navigates immediately after success. Toast notification persists across route changes by design. |
| **PrimeNG UI Migration** | ✅ Replaced all custom CSS/SCSS with PrimeNG component library (v17.18.15). Migrated every component: Navbar → `p-menubar`, Toast → `p-toast`, Spinner → `p-progressSpinner`, Dashboard → `p-card`/`p-table`/`p-tag`, Products → `p-table`/`p-toolbar`/`p-dropdown`/`p-confirmDialog`/`p-inputNumber`, Orders → `p-card`/`p-table`/`p-tag`/`p-dropdown`, Customers → `p-table`/`p-dialog`/`p-checkbox`/`p-toolbar`, Broadcast → `p-card`/`p-dropdown`/`p-table`/`p-message`. Theme: Lara Light Indigo. |
| **UI Polish (Minimal `::ng-deep`)** | ✅ All PrimeNG overrides moved to global `styles.scss`. One scoped `::ng-deep` in `product-list.component.scss` for dropdown filter panel styling (required because `appendTo="body"` renders the panel outside the component — standard PrimeNG practice). Comprehensive overrides for navbar, toolbar, card, table, tag, button, dropdown, input, dialog, checkbox, progress spinner. Design system: indigo accent (#6366f1), dark navbar (#1a1a2e), gold brand (#e0c097), Inter font. |
| **Button & Input Refinements** | ✅ Buttons: 10px/20px padding, font-weight 600, 8px gap between icon and label. Search input: icon at 14px with 40px left padding. Dropdowns: polished label padding, hover border color, rounded panel items with highlight. |
| **Contained Filter Bars** | ✅ `.filters-bar` component — white background container with border, rounded corners, shadow for products and customers pages. Replaced floating loose filters with a contained card feel. |
| **Broadcast Page Redesign** | ✅ Complete redesign with 2-column layout: form card on left with section header + form grid + send action area, stats sidebar on right (active subscribers, total broadcasts, messages sent). History section with proper header. Responsive grid collapses to single column under 900px. |
| **CSS Design Tokens (L6/L7 Fix)** | ✅ Rewrote `styles.scss` with 75+ CSS custom properties (`--ls-brand-gold`, `--ls-accent`, `--ls-bg-card`, `--ls-shadow-md`, `--ls-radius-xl`, etc.). Removed all 60+ `!important` declarations — PrimeNG overrides now use `body .p-*` prefix for natural specificity instead of brute-force `!important`. Full theming support via `:root` variables. |
| **Shared Utilities (L8 Fix)** | ✅ Eliminated code duplication across frontend and backend: (1) `shared/utils/severity.utils.ts` — shared `getStatusSeverity()` + `getStatusButtonSeverity()` used by dashboard + orders. (2) `shared/services/template-loader.service.ts` — shared `TemplateLoaderService` with caching, validation, language lookup, used by broadcast + customers. (3) `Extensions/MappingExtensions.cs` — `Product.ToDto()`, `Order.ToDto()`, `OrderItem.ToDto()` extension methods used by ProductService, OrderService, DashboardService. |
| **Accessibility (L9 Fix)** | ✅ Products `<p-tag>` toggle: `role="button"`, `tabindex="0"`, Enter/Space keyboard handlers, `aria-label`. Order expand `<div>`: `role="button"`, `tabindex="0"`, `aria-expanded`, keyboard handlers. Loading spinner: `role="status"` + `aria-live="polite"`. Skip-to-content link in app shell with focus-visible CSS. |
| **Form Validation (L10 Fix)** | ✅ Product form: inline `<small class="p-error">` messages for all 5 required fields, PrimeNG `ng-invalid`/`ng-dirty` red borders on submit, per-field toast notifications, `submitted` flag prevents premature errors. |
| **Unsaved Changes Guard (L11 Fix)** | ✅ `CanDeactivateFn` route guard (`core/guards/unsaved-changes.guard.ts`) with `confirm()` dialog. `@HostListener('window:beforeunload')` for tab close. JSON snapshot dirty detection. `savedSuccessfully` bypass. Wired to `/products/new` and `/products/edit/:id`. |
| **Reactive Forms Migration** | ✅ Converted entire frontend from template-driven (`FormsModule`/`ngModel`/`NgForm`) to Reactive Forms (`ReactiveFormsModule`/`FormGroup`/`FormBuilder`/`Validators`). All 5 feature components converted: (1) Broadcast — `broadcastForm` with custom `templateValidator`. (2) Product Form — `productForm` with `Validators.required`/`min`, unsaved changes guard preserved via JSON snapshot. (3) Product List — `filterForm` for search/category/brand filters. (4) Orders — `filterForm` for status filter. (5) Customers — 4 form groups (`addCustomerForm`, `importForm`, `broadcastForm`, `filterForm`) with pattern validators and custom template validator. Only `FormsModule` retained in customers for dynamic table row checkbox `[(ngModel)]` bindings (Angular best practice for dynamic lists). Zero `@ViewChild(NgForm)`, zero `#ref="ngForm"` patterns remain. `markAllAsTouched()` on every form submission. `submitted` flags prevent premature error display. |
| **Filterable Dropdown UX** | ✅ Product filter dropdowns (Category, Brand) use PrimeNG's built-in `[filter]="true"` with `filterBy="label"` — opens a search panel inside the dropdown to narrow options. `[showClear]="true"` adds an X to clear the selected value. `[resetFilterOnHide]="true"` clears filter text when panel closes. Custom `filtericon` template replaces the default search icon with a `pi pi-times` X button that calls `clearDropdownFilter()` to reset filter text programmatically. `emptyFilterMessage="No results found"` for invalid searches. Dedicated Search (`pi pi-search`) and Refresh (`pi pi-refresh`) buttons — API call fires only on button click or Enter key, never on typing. Broadcast template dropdown uses simple `<p-dropdown>` with click-to-select (no filter needed for short template lists). |
| **Memory Leak Fix (M5)** | ✅ No `valueChanges` subscriptions remain in product-list (simplified to button-triggered search). HTTP `subscribe()` calls auto-complete — no leak risk. All observable patterns are leak-safe by design. |
| **Dead Code Cleanup (L13/L14)** | ✅ Removed unused `Router` injections from navbar and customers components. Removed unused `filteredCustomers` variable from customers. Removed unused `sharedButtonSeverity` import alias from orders. Consolidated duplicate `AbstractControl`/`ValidationErrors` import in customers. Removed dead `send-btn` CSS class reference from broadcast. |
| **Accessibility Improvements** | ✅ Added `for`/`id` pairs on all customers dialog labels (add customer, broadcast dialogs). Added `aria-label` on table header checkbox ("Select all customers") and row checkboxes ("Select customer {name}"). Added `aria-label` on product and customer search inputs. |
| **JWT Authentication (C1 Fix)** | ✅ Full authentication layer: Backend — `AuthController` with `POST /api/auth/login`, BCrypt password verification against `AdminUsers` PostgreSQL table (case-sensitive exact match), JWT Bearer token generation (24h expiry via `TokenExpiryHours` constant), `[Authorize]` attribute on all admin controllers (Products, Orders, Customers, Dashboard, Broadcast), Payment and WhatsApp webhook remain public. `AdminUser` model with `AdminUserConfiguration` Fluent API config. Auto-seeds `Admin` user on first startup. Frontend — `AuthService` (login/logout/token management), `AuthGuard` (`CanActivateFn` protecting all admin routes), `AuthInterceptor` (attaches Bearer token to all requests), animated login page with background video, leather texture overlay, frosted glass card, inline error messages, and smooth transitions. Navbar redesigned with username pill badge + round red power-off logout button pushed to far right. |
| **DB-Based Admin Credentials** | ✅ Moved admin credentials from `appsettings.json` to PostgreSQL `AdminUsers` table. BCrypt password hashing with `BCrypt.Net-Next`. Credentials auto-seeded on first startup via `Program.cs`. Removed `Admin` section from appsettings.json entirely. |
| **Code Quality Audit Fixes** | ✅ 10 fixes applied from comprehensive codebase audit: (1) Error interceptor skips toast for login 401s (prevents double notification). (2) Auth interceptor removed unused `Router` import, fixed doc comment. (3) Login component removed unused `PasswordModule`. (4) Login HTML changed from "Protected by JWT Authentication" to "Secure Admin Access" (info leakage). (5) App component fixed type narrowing for `NavigationEnd`, removed empty `styleUrl`. (6) Product model `Description` MaxLength aligned to 2000 (matching DTO). (7) Product form categories fetched dynamically from API instead of hardcoded. (8) Product list added error handlers on `toggleActive`, `deleteProduct`, `getCategories`, `getBrands`. (9) Orders component added error handler on `updateStatus` with status revert on failure. (10) AuthController extracted `TokenExpiryHours = 24` constant. |
| **Broadcast Status Polling** | ✅ Added `GET /api/broadcast/{id}/status` endpoint. Frontend polls every 1s for up to 30s after sending. Shows real-time results: all-failed (red error banner), partial (warning), all-success (green). Custom styled status banners with gradient backgrounds, icons, slideDown animation, and dismissible close button. Dark styled toast notifications positioned 60px from top. |
| **Performance Audit & Fixes (5000+ Scale)** | ✅ Comprehensive deep audit of frontend (26 issues) and backend (30 issues). Fixes applied: (1) Customer table pagination — 25/50/100 rows per page with page report (client-side, correct for selection use-case). (2) Orders server-side pagination — `PaginatedResult<T>` model, `GET /api/orders?page=1&pageSize=25` (clamped 1–100), PrimeNG `p-paginator` on frontend. (3) `selectedCount` getter replaced with cached `_selectedCount` counter — O(1) instead of O(n) on every change detection. (4) `getTotalSent()` method in template replaced with cached `totalSent` property. (5) `setInterval` memory leak fixed — `ngOnDestroy` clears polling interval. (6) Orders `*ngFor` now has `trackBy: trackByOrderId`. (7) BulkImport N+1 fixed — single query loads all phone numbers into HashSet, then O(1) lookups. (8) Dashboard uses sequential awaits with `AsNoTracking()` — EF Core DbContext is NOT thread-safe so `Task.WhenAll` is incorrect. (9) SemaphoreSlim in BroadcastBackgroundService now properly disposed with `using`. (10) WhatsApp notifications in OrderService and PaymentService wrapped in try/catch — prevents 500 errors on successful DB operations. (11) Razorpay signature verification implemented — HMAC-SHA256 mandatory when `KeySecret` is configured, skipped with warning in dev. (12) XSS in PaymentController fully fixed — `WebUtility.HtmlEncode()` on OrderNumber, CustomerPhone, ProductName. (13) DB indexes added: `IsSubscribed`, `CreatedAt` (customers), `Status`, `CreatedAt`, `IsPaid` (orders), `IsActive` (products). |
| **WhatsApp Business Setup** | ✅ Permanent token with Admin System User "Leathershop" under "Leather Shop" Business Portfolio (ID: 1270862431810807). WABA ID: 2151682048973965, Phone Number ID: 1055485577637232, Phone: +91 79043 03876. 3 custom templates created (`shop_deals`, `order_update`, `store_notification`) — all PENDING Meta approval. Phone number registered via Cloud API `/register` endpoint. |
| **Railway Deployment** | ✅ Full cloud deployment: (1) `Dockerfile` — multi-stage build (SDK 8.0 → ASP.NET 8.0 runtime). (2) `railway.toml` — build config with `watchPatterns`, health check on `/swagger/index.html`, restart-on-failure policy. (3) `ServiceCollectionExtensions.cs` — `AddDatabase()` auto-parses Railway `DATABASE_URL` URI format to Npgsql connection string, `AddCorsPolicies()` reads `FRONTEND_URL` env var. (4) `Program.cs` — reads `PORT` env var, Swagger enabled in all environments (health check). (5) `appsettings.Production.json` — placeholder values, actual secrets in Railway env vars. (6) `environment.prod.ts` — API URL set to `https://leathershop-production.up.railway.app/api`. (7) PostgreSQL on Railway with persistent volume. Public URL: `leathershop-production.up.railway.app`. |
| **Vercel Frontend Deployment** | ✅ Angular admin panel deployed to Vercel: Root directory `LeatherShopAdmin`, framework preset Angular, build command `ng build --configuration production`, output `dist/leather-shop-admin/browser`. Auto-deploys from GitHub `main` branch. |
| **Image Upload** | ✅ Server-side file upload: `POST /api/products/upload-image` accepts multipart file, validates type (JPG/PNG/WebP/GIF) and size (< 5 MB), saves to `wwwroot/uploads/` with GUID filename, returns relative path. `app.UseStaticFiles()` serves uploaded images. Frontend: clickable browse dropzone replaces URL text input, instant local preview via `FileReader`, remove button (×) to clear. `[Url]` DTO validators removed since images are now server-relative paths. |
| **Duplicate Product Name Validation** | ✅ Async validator on product name field: `GET /api/products/check-name?name=X&excludeId=Y` endpoint performs case-insensitive DB lookup (excludes current product on edit). Frontend: 300ms debounced `AsyncValidator` with `timer()` + `switchMap()`, spinner while checking, inline error "A product with this name already exists". Submit button disabled while validation pending. |
| **Logout + Unsaved Changes Guard Fix** | ✅ Fixed bug where clicking Logout on a dirty form, then clicking "Stay", would still log the user out on next navigation. Root cause: `auth.logout()` cleared localStorage tokens immediately before `canDeactivate` could block navigation. Fix: `AuthService.clearSession()` (tokens only, no navigate) + `navbar.logout()` navigates first via `router.navigate(['/login'])`, clears tokens only in `.then()` callback if navigation succeeded. Login component skips "already logged in" redirect when arriving from logout via `NavigationExtras.state`. |
| **WhatsApp Product Image** | ✅ Product images now display in WhatsApp chatbot when a customer views product details. **Implementation chain:** (1) `IWhatsAppService.SendImageMessage(to, imageUrl, caption)` — new interface method. (2) `WhatsAppService.SendImageMessage()` — sends WhatsApp Cloud API `image` message type with `link` (public URL) + `caption` (product details text). (3) `ChatBotService.SendProductDetails()` — if `product.ImageUrl` is set, constructs full URL using `App:BaseUrl` config with fallback to `RAILWAY_PUBLIC_DOMAIN` env var (auto-provided by Railway), sends image with details as caption, then sends action buttons as separate message. Falls back to text-only on failure. Caption truncated to 1024 chars (WhatsApp limit). **Key debug history:** Initial deploy failed with "Param image['link'] is not a valid URL" because `App:BaseUrl` was set to placeholder `WILL_BE_SET_BY_RAILWAY_ENV_VAR` instead of actual URL. Fixed by adding `RAILWAY_PUBLIC_DOMAIN` fallback. **Files:** `IWhatsAppService.cs`, `WhatsAppService.cs`, `ChatBotService.cs` (lines ~258-305). |
| **Railway Upload Volume** | ✅ Railway Volume (`leathershop-volume`) mounted at `/app/wwwroot/uploads` on the LeatherShop service. Persists uploaded product images across redeployments (Railway's default filesystem is ephemeral — wiped on every deploy). **Setup:** Railway Dashboard → Architecture → + Create → Volume → attach to LeatherShop → mount path `/app/wwwroot/uploads`. Cost: ~$0.25/GB/month (included in $5/mo Hobby plan credit). **Important:** Images uploaded before the volume was attached are lost — must re-upload after volume setup. |
| **Exception Handling Audit** | ✅ Full audit of all 15 `catch` blocks across the codebase — **zero exception swallowing found**. All catch blocks either: (a) log the exception with `_logger.LogError`/`LogWarning` + re-throw or return error response, (b) are intentional graceful degradation (e.g., WhatsApp notification failure doesn't block order creation, image send failure falls back to text). **Intentional patterns:** (1) `WhatsAppWebhookController` returns `Ok()` even on error — required because Meta retries on non-200 responses. (2) `PaymentService`/`CustomerService` catch WhatsApp notification failures with `LogWarning` — notifications are best-effort, the core operation (payment/customer creation) must succeed. (3) `ChatBotService` image fallback catches with `LogWarning` and falls back to text-only. (4) `BroadcastBackgroundService.SaveProgressAsync` catches with `LogWarning` — progress save is best-effort, final save catches up. **Previously fixed:** `OrderService.cs` had an empty `catch { }` (P4 in audit) — was already fixed with `_logger.LogWarning`. |
| **2-Way Chat + Order Notifications (SignalR)** | ✅ Full real-time chat and notification system. **Approach:** (A) **SignalR WebSocket hub** (`/hubs/notifications`) for real-time push — no polling needed. JWT-authenticated via query string token. (B) **Order notifications** — when customer completes payment, `PaymentService` pushes `NewOrder` event via SignalR to all connected admin browsers + sends WhatsApp message to shop owner (`OwnerPhone` config). Navbar bell icon shows badge count + overlay panel with notification list. (C) **2-way chat** — all WhatsApp messages (incoming + outgoing) stored in `ChatMessages` table. `WhatsAppWebhookController` saves incoming messages + pushes via SignalR. `ChatBotService.BotSend*` wrapper methods save all bot outgoing messages + push via SignalR. Admin chat page shows conversation sidebar + WhatsApp-style message thread. Admin replies sent via `ChatController.Send` → `ChatService.SendMessageAsync` → WhatsApp API. (D) **Bot pause/resume** — when admin sends a message, chatbot auto-pauses for that customer (30 min default). `Customer.IsBotPaused` + `BotPausedUntil` fields. Webhook checks pause status before routing to chatbot. Admin can manually pause/resume. Bot auto-resumes when `BotPausedUntil` expires. **New files:** Backend: `ChatMessage.cs`, `ChatMessageConfiguration.cs`, `IChatService.cs`, `ChatService.cs`, `NotificationHub.cs`, `ChatController.cs`, `ChatDtos.cs`. Frontend: `signalr.service.ts`, `chat/` feature module (model, service, routes, chat-page component). Modified: `WhatsAppWebhookController` (save + push + bot pause check), `ChatBotService` (BotSend* wrappers), `PaymentService` (owner notification + SignalR push), `Customer.cs` (IsBotPaused, BotPausedUntil), `Program.cs` (AddSignalR, MapHub, JWT SignalR events), `ServiceCollectionExtensions` (IChatService, AllowCredentials), `navbar` (bell + Chat menu + SignalR), `environment*.ts` (hubUrl), `app.routes.ts` (/chat route). **DB migration:** `AddMissingChatColumnsAndTable` — creates `ChatMessages` table + adds `IsBotPaused`/`BotPausedUntil` to `Customers` + composite indexes. |
| **Chat & Customer Management Enhancements** | ✅ Comprehensive data management features. **Approach:** (A) **Auto-delete old chats** — `ChatCleanupBackgroundService` (hosted service) runs every 24 hours, uses `ExecuteDeleteAsync` to bulk-delete `ChatMessages` older than 30 days. Zero N+1, zero memory overhead (no entity loading). Registered via `AddHostedService`. (B) **Manual chat delete** — `DELETE /api/chat/{customerId}/messages` endpoint + delete button (trash icon) in chat header with confirmation dialog. Removes all messages for a customer conversation. (C) **Customer delete** — `DELETE /api/customers/{id}` endpoint + delete button in customer table with confirmation dialog. Cascade deletes all related data (orders, cart items, chat messages) via FK configuration. (D) **Customer edit** — `PUT /api/customers/{id}` endpoint + edit button (pencil icon) in customer table → dialog with name, address, subscription toggle. **No WhatsApp message is sent on edit** — purely a DB update. (E) **Address mandatory in UI** — Add Customer dialog now requires address (min 10 chars). Edit Customer dialog also requires address. Address field uses `<textarea>` for multi-line input. (F) **Bot asks address at checkout** — `Customer.PendingAction` field tracks bot conversational state (`"awaiting_address"`, `"confirming_address"`). When customer types "checkout" and has no address, bot asks for shipping address before creating the order. When address already exists, bot shows an **order summary with the saved address** and presents **"✅ Confirm" / "✏️ Change Address"** interactive buttons — customer can review and correct their address on every order. Address saved to `Customer.Address` and copied to `Order.ShippingAddress`. If customer taps an interactive button while awaiting address, the prompt is cancelled gracefully. Order summary now includes shipping address. **DB migration:** `AddCustomerPendingAction` — adds `PendingAction` varchar(50) nullable column to `Customers`. **New files:** `ChatCleanupBackgroundService.cs`. **Modified:** `Customer.cs` (PendingAction), `CustomerDtos.cs` (UpdateCustomerDto), `ICustomerService.cs` (UpdateAsync, DeleteAsync), `CustomerService.cs`, `CustomersController.cs` (PUT, DELETE), `IChatService.cs` (DeleteConversationAsync), `ChatService.cs`, `ChatController.cs` (DELETE), `ChatBotService.cs` (address flow + confirmation step), `ServiceCollectionExtensions.cs` (cleanup service). Frontend: `customer.model.ts` (UpdateCustomer), `customer.service.ts` (update, delete), `customers.component.ts/html/scss` (edit dialog, delete dialog, address field, action buttons), `chat.service.ts` (deleteConversation), `chat-page.component.ts/html` (delete conversation dialog + button). |

---

## 🔮 Future Pending Tasks (Feb 25, 2026 — Deep Analysis)

Full deep analysis of the entire codebase. These are **real issues** found by reading every file — organized by priority for future implementation.

### 🔴 CRITICAL — Must Fix Before Production Use

| # | Issue | Location | Details | Fix |
|---|-------|----------|---------|-----|
| F1 | ~~**Secrets committed to Git**~~ | ~~`appsettings.json`~~ | ✅ **FIXED** — `appsettings.json` now contains only empty placeholders (safe to commit). All secrets moved to `appsettings.Local.json` (gitignored). `Program.cs` auto-loads local config on startup. `Admin:SeedPassword` read from config instead of hardcoded. `UserSecretsId` added to `.csproj`. `appsettings.Local.json.example` template committed. Production secrets via Railway env vars. **Note:** Previously exposed credentials should still be rotated. | ~~Move to env vars~~ ✅ Done |
| F2 | **Payment bypass when KeySecret is empty** | `PaymentService.cs` | When `Razorpay:KeySecret` is empty or placeholder, signature verification is **completely skipped**. Attacker can call `POST /api/payment/verify` with any `paymentId`/`orderId` to mark orders as paid for free. | Fail closed — if `KeySecret` is not configured, **reject** the payment instead of skipping verification. |
| F3 | **WhatsApp webhook signature not validated** | `WhatsAppWebhookController.cs` | Meta sends `X-Hub-Signature-256` header on every POST. The controller never validates it. Attackers can POST fabricated messages to create fake orders. | Validate `X-Hub-Signature-256` using HMAC-SHA256 with your App Secret. |
| F4 | **Race condition: overselling during checkout** | `ChatBotService.cs` `PlaceOrder()` | Stock checked then decremented without DB-level locking. Two concurrent checkouts can both pass the stock check and oversell. | Use `UPDATE Products SET StockQuantity = StockQuantity - @qty WHERE Id = @id AND StockQuantity >= @qty` and check affected rows, or add a `RowVersion` concurrency token. |

### 🟠 HIGH — Data Integrity & Bugs

| # | Issue | Location | Details | Fix |
|---|-------|----------|---------|-----|
| F5 | **Bot pause expiry never saved to DB** | `ChatService.cs` `IsBotCurrentlyPaused()` | Static method mutates `customer.IsBotPaused = false` when timer expires but **never calls `SaveChangesAsync()`**. The DB still says paused; every subsequent request re-evaluates in-memory only. | Save changes inside `IsBotPausedAsync()` when it detects expiry, or move auto-resume to a method that explicitly saves. |
| F6 | **Duplicate webhook processing** | `WhatsAppWebhookController.cs` | WhatsApp delivers webhooks at-least-once. No idempotency check on `message.Id`. Each duplicate creates duplicate `ChatMessage` rows, duplicate bot responses, and potentially duplicate orders. | Store and check `message.Id` before processing. Skip if already seen. |
| F7 | **First message from new customer lost** | `WhatsAppWebhookController.cs` | If the customer doesn't exist (first-ever message), the `if (customer != null)` block skips saving the incoming message. The chatbot creates the customer, but their initial message is never stored in chat history. | Re-fetch the customer after `ProcessMessage` and save the incoming message, or restructure the flow. |
| F8 | **`int.Parse` on interactive IDs** | `ChatBotService.cs` | `int.Parse(input.Replace("prod_", ""))` throws `FormatException` on malformed input like `prod_abc`. Caught by outer try-catch but sends generic error. | Use `int.TryParse` instead. |
| F9 | **SignalR stale token on reconnect** | `signalr.service.ts` | `accessTokenFactory: () => token` captures the token value at connection time. When SignalR auto-reconnects after network issues, it reuses the stale token — fails silently if the token expired. | Change to `accessTokenFactory: () => this.auth.getToken() \|\| ''` to read fresh token on each reconnect. |
| F10 | ~~**Double toast notifications**~~ | `error.interceptor.ts` + all components | ✅ **FIXED** — Removed all component-level `notification.error()` calls that duplicated the interceptor toast. 13 locations fixed across `product-list.component.ts`, `product-form.component.ts`, `orders.component.ts`, `customers.component.ts`. Components now only manage UI state (loading flags, reverts) in error handlers; the interceptor shows the user-facing toast using the API message. | ~~Remove per-component toasts~~ ✅ Done |
| F11 | **Orders paginator visual desync** | `orders.component.html` | `<p-paginator>` has no `[first]` binding. When filter resets `currentPage = 1`, paginator UI still shows the old page number. | Add `[first]="(currentPage - 1) * pageSize"` to the paginator element. |
| F12 | **SignalR not stopped on 401 logout** | `error.interceptor.ts` | When interceptor detects 401 and calls `auth.logout()`, `signalR.stop()` is never called (only called from navbar logout). Connection keeps running with invalid token. **FIXED — interceptor now calls `signalR.stop()` before `auth.logout()`.** | Call `signalR.stop()` inside `auth.logout()`. |
| F13 | **Multiple 401s trigger concurrent navigations** | `error.interceptor.ts` | If multiple API calls are in-flight when token expires, each 401 triggers `auth.logout()` → `router.navigate`. Multiple simultaneous navigations cause `NavigationCancel` errors. | Add an `isLoggingOut` guard flag in `AuthService` to ensure logout/redirect fires only once. |

### 🟡 MEDIUM — Performance & Code Quality

| # | Issue | Location | Details | Fix |
|---|-------|----------|---------|-----|
| F14 | **N+1 query in conversations list** | `ChatService.cs` `GetConversationsAsync()` | For each customer, fires 3 additional queries (last message, last admin message, unread count) in a `foreach` loop. 100 customers = 300+ SQL queries. | Rewrite using a single query with `GroupBy` or window functions. |
| F15 | **Product hard-delete crashes on ordered products** | `ProductService.cs` | `_db.Products.Remove()` deletes the entity. `OrderItem → Product` FK uses `DeleteBehavior.Restrict` — so deleting a product that has been ordered throws a DB exception. | Use soft-delete (`IsActive = false`) instead. Check for existing orders before hard delete. |
| F16 | **Stale cart prices at checkout** | `ChatBotService.cs` `PlaceOrder()` | Between adding to cart and checking out (could be hours/days), product prices and active status can change. Order uses current price, not the price at the time of adding. | Verify `product.IsActive` in `PlaceOrder()`. Consider storing price on `CartItem` at add-time. |
| F17 | **Swagger exposed in production** | `Program.cs` | Swagger UI exposes the full API documentation in production (used as health check). Gives attackers a complete map of all endpoints. | Only enable in Development. Use a proper `/health` endpoint for health checks. |
| F18 | **No server-side pagination for products** | `ProductService.cs`, `product-list.component.ts` | `GetAllAsync()` returns ALL products with no pagination. With hundreds of products, this becomes slow. | Add `PaginatedResult<T>` to products like orders has. |
| F19 | **No server-side pagination for customers** | `CustomerService.cs` | `GetAllAsync()` returns ALL customers with no pagination. With 5000+ customers, response payload is huge. | Add server-side pagination. |
| F20 | **`DeleteConversationAsync` loads all messages** | `ChatService.cs` | Uses `ToListAsync()` then `RemoveRange()` — loads all messages into memory. Large conversations waste memory. | Use `ExecuteDeleteAsync()` like the cleanup service does. |
| F21 | **`.ToLower()` kills DB indexes** | `CustomerService.cs`, `ProductService.cs`, `ChatService.cs` | `.ToLower().Contains()` generates `LOWER()` SQL, preventing PostgreSQL from using indexes. | Use `EF.Functions.ILike()` for case-insensitive search on Npgsql. |
| F22 | **`PaginatedResult.TotalPages` divide-by-zero** | `PaginatedResult.cs` | `TotalCount / (double)PageSize` when `PageSize = 0` throws. Only `OrdersController` clamps `pageSize >= 1`; other callers don't validate. | Guard: `PageSize <= 0 ? 0 : (int)Math.Ceiling(...)`. |
| F23 | ~~**Admin seed password hardcoded**~~ | ~~`Program.cs`~~ | ✅ **FIXED** — `Program.cs` now reads `Admin:SeedPassword` from configuration (appsettings.Local.json / env var / user-secrets). Throws clear `InvalidOperationException` if not configured and no admin exists in DB. No hardcoded password in source. Change-password endpoint still pending. | ~~Accept from env var~~ ✅ Done (change-password endpoint still TODO) |
| F24 | **Chat height off by 14px — double scrollbar** | `chat-page.component.scss` | Uses `height: calc(100vh - 70px)` but `.main-content` has `padding-top: 84px`. Overshoots by 14px, causing a page-level scrollbar alongside the chat scrollbar. | Change to `height: calc(100vh - 84px)`. |
| F25 | **Auth guard doesn't preserve return URL** | `auth.guard.ts` | Redirects to `/login` without passing the intended URL. After login, user always lands on `/dashboard` instead of their bookmarked page. | Pass `returnUrl` as query param, redirect after login. |
| F26 | **Chat search timeout not cleared on destroy** | `chat-page.component.ts` | `setTimeout` in `onSearch()` not cleared in `ngOnDestroy()`. If component destroys while pending, `loadConversations()` fires after destruction. | Add `clearTimeout(this.searchTimeout)` in `ngOnDestroy()`. |
| F27 | **Product form — no error handler for `getProduct()`** | `product-form.component.ts` | In edit mode, if the product doesn't exist or request fails, form shows empty with no error message. | Add `error` handler — show message and navigate back. |
| F28 | **Template loader race condition** | `template-loader.service.ts` | Both `BroadcastComponent` and `CustomersComponent` call `loadTemplates()` in `ngOnInit()`. Quick navigation causes duplicate HTTP requests. | Cache the Observable with `shareReplay`, or check loading state. |

### 🔵 LOW — Nice to Have

| # | Issue | Location | Details | Fix |
|---|-------|----------|---------|-----|
| F29 | **No rate limiting** | All controllers | No rate limiting on login (brute-force), payment, webhook, or admin endpoints. | Add `Microsoft.AspNetCore.RateLimiting` middleware, especially on auth and payment. |
| F30 | **No health check endpoint** | `Program.cs` | Uses Swagger as health check (exposes API docs). No proper `/health` endpoint. | Add `app.MapHealthChecks("/health")` with `AddHealthChecks()`. |
| F31 | **Order status — no transition validation** | `OrderService.cs` | Any status can transition to any other (e.g., `Delivered` → `Pending`). | Implement a state machine that enforces valid transitions. |
| F32 | **No order cancellation refund** | `OrderService.cs` | Cancelling a paid order restores stock but doesn't trigger a Razorpay refund. | Add Razorpay refund API call, or at minimum warn the admin. |
| F33 | **Navbar notification bell — not keyboard accessible** | `navbar.component.html` | Bell is a `<div>` with `(click)` only. No `role`, `tabindex`, or keyboard handlers. | Add `role="button" tabindex="0" aria-label="Notifications"` + keyboard handlers. |
| F34 | **Chat conversations — not keyboard accessible** | `chat-page.component.html` | Conversation items are `<div>` with `(click)` only. | Add `tabindex="0" role="button"` + keyboard handlers. |
| F35 | **Wildcard route goes to login, not 404** | `app.routes.ts` | `{ path: '**', redirectTo: 'login' }` — authenticated users hitting invalid URLs get redirected to login instead of seeing "page not found". | Add a `NotFoundComponent` on the wildcard route. |
| F36 | **Dashboard never auto-refreshes** | `dashboard.component.ts` | Data fetched once on init. Admin leaving the tab open sees stale stats. SignalR `newOrder$` events are not used to refresh. | Listen to `signalR.newOrder$` and reload, or add a refresh button. |
| F37 | **Login password toggle — no aria-label** | `login.component.html` | Toggle button has no accessible label. Screen readers read "button" with no context. | Add `[attr.aria-label]="showPassword ? 'Hide password' : 'Show password'"`. |
| F38 | **Orders — status update without confirmation** | `orders.component.html` | Clicking "Cancelled" immediately fires `updateStatus()` with no confirmation dialog. Accidental clicks are irreversible. | Add confirmation dialog for destructive transitions. |
| F39 | **WhatsApp list row title truncation** | `ChatBotService.cs` | Truncates at 24 chars with no ellipsis. Product names get cut mid-word. | Truncate at 21 chars + add `"..."`. |
| F40 | **`PhoneNumberHelper.Normalize` doesn't validate** | `PhoneNumberHelper.cs` | Strips formatting but doesn't verify the result is numeric. Letters can slip through. | After stripping, validate with `long.TryParse` or regex `^\d{7,15}$`. |
| F41 | **Add Customer requires address, but bulk import doesn't** | `customers.component.ts` | Add dialog has `Validators.required` for address (min 10 chars), but bulk import creates customers with no address. Inconsistent. | Either make address optional in add dialog, or add address support to bulk import. |
| F42 | **No `OnPush` change detection** | All Angular components | All use default change detection. Extra re-renders on every browser event. | Add `changeDetection: ChangeDetectionStrategy.OnPush` to each component. |
| F43 | **Broadcast layout breaks on mobile** | `broadcast.component.scss` | Fixed 2-column grid (`1fr 300px`) with no responsive breakpoint. Sidebar squishes on small screens. | Add `@media (max-width: 768px) { grid-template-columns: 1fr; }`. |
| F44 | **Logging — console only** | `Program.cs` | No structured logging. Need Serilog or similar for log files, search, and alerting. | Add Serilog with file/JSON/cloud sinks. |

### 🆕 New Findings (Deep Analysis — Feb 26, 2026)

27 new issues found by reading every file line-by-line. Cross-referenced against all existing items above — zero duplicates.

#### Backend — New Issues

| # | Severity | Issue | Location | Details | Fix |
|---|----------|-------|----------|---------|-----|
| F45 | **High** | **Payment re-verification — no `IsPaid` guard** | `PaymentService.cs` `VerifyPaymentAsync()` | After fetching the order, proceeds directly to set `IsPaid = true` without checking if already paid. Re-calling verify on a paid order triggers duplicate WhatsApp notifications to owner + duplicate SignalR push to admin. `GetPaymentPageDataAsync` checks `IsPaid` but the verify endpoint does not. | Add `if (order.IsPaid) return already-paid result;` before processing. |
| F46 | **High** | **Welcome text to new customer — WhatsApp rejects outside 24h** | `ChatBotService.cs` `ProcessMessage()` | When a new customer is created, bot sends a plain text welcome message. WhatsApp requires **template messages** to initiate conversations outside the 24-hour window. If the customer's first message opens the window but the welcome response is sent after a delay (e.g., bot processing takes >24h due to downtime), it will fail silently. | Use an approved template for the welcome message, or ensure it's always within the response window. |
| F47 | **High** | **Payment URL broken when `App:BaseUrl` not configured** | `ChatBotService.cs` `PlaceOrder()` | Payment link constructed as `{baseUrl}/api/payment/{orderId}`. If `App:BaseUrl` config is empty/placeholder, customer receives a broken link in WhatsApp. Unlike image URLs, there's no `RAILWAY_PUBLIC_DOMAIN` fallback here. | Add the same `RAILWAY_PUBLIC_DOMAIN` fallback used for images. |
| F48 | **High** | **Product Description MaxLength still mismatched in Fluent API** | `ProductConfiguration.cs` L18-19 | H5 aligned the `[MaxLength]` attribute on `Product.cs` to 2000, but the Fluent API config still says `.HasMaxLength(1000)`. **EF Core Fluent API takes precedence** — DB column is still limited to 1000 chars despite model saying 2000. Descriptions >1000 chars silently truncated or throw. | Change `.HasMaxLength(1000)` to `.HasMaxLength(2000)` and create a migration. |
| F49 | **Medium** | **`UpdateStatusAsync` ambiguous return value** | `OrderService.cs` | Returns `false` for both "order not found" and "invalid status string". Controller can't distinguish between 404 and 400 — always returns same error message. | Return a result enum or throw different exceptions for not-found vs invalid-status. |
| F50 | **Medium** | **Payment page `RazorpayKeyId` not encoded** | `PaymentController.cs` L82 | `key: '{data.RazorpayKeyId}'` is injected raw into JavaScript. Other values (`OrderNumber`, `CustomerPhone`) are HTML-encoded but the Razorpay key is not. Config value with a single quote (`'`) breaks JS syntax. Low risk since it's server config, but inconsistent. | Apply `JavaScriptEncoder.Default.Encode()` or at minimum `HtmlEncode`. |
| F51 | **Medium** | **Payment page IDOR — sequential integer order IDs** | `PaymentController.cs` | Payment page URL is `/api/payment/{orderId}` with sequential integers. Attacker can enumerate order IDs to view other customers' order details (amount, phone, products). | Use `OrderNumber` (GUID-based) instead of `Id` in payment URLs. |
| F52 | **Medium** | ~~**Webhook `entry.Changes` not null-checked**~~ | ~~`WhatsAppWebhookController.cs`~~ | ✅ **FIXED** — Added `if (entry.Changes == null) continue;` before the inner `foreach` loop. Prevents `NullReferenceException` when WhatsApp sends an entry with null Changes array. Remaining entries in the batch continue processing normally. | ~~Add null-check~~ ✅ Done |
| F53 | **Medium** | ~~**Customer deletion cascade-deletes all order history**~~ | ~~`AppDbContext` FK config~~ | ✅ **FIXED** — Changed Customer→Orders FK from `DeleteBehavior.Cascade` to `DeleteBehavior.Restrict` in both `CustomerConfiguration.cs` and `OrderConfiguration.cs`. `CustomerService.DeleteAsync()` returns a `DeleteCustomerResponse` with `DeleteCustomerResult` enum (`Deleted` / `NotFound` / `HasOrders`) — no exceptions for flow control. Controller uses pattern matching (`switch` expression) for 200/404/409 responses. Frontend delete dialog properly closes on error. Cart items and chat messages still cascade-delete (transient data). Migration `RestrictCustomerOrderDeletion` created. | ~~Restrict deletion~~ ✅ Done |
| F54 | **Low** | **Category-to-ID round-trip collision in chatbot** | `ChatBotService.cs` | Category names converted to button IDs via `cat_leather_wallets`. Categories with underscores vs spaces collide (e.g., "Leather Wallets" and "Leather_Wallets" generate the same ID). | Use a hash or index-based ID instead of name-based. |
| F55 | **Low** | **Broadcast history hardcoded `Take(20)`** | `BroadcastService.cs` | `GetBroadcastHistoryAsync()` always returns last 20 broadcasts with no pagination. Older broadcasts are inaccessible. | Add pagination parameters. |
| F56 | **Low** | **Abandoned cart items never expire** | `CartItem` model | Cart items have `AddedAt` but no expiry logic. Carts accumulate indefinitely, holding "reserved" items that skew stock perception. | Add a cleanup job similar to `ChatCleanupBackgroundService`, or expire items after 24-48h. |
| F57 | **Low** | **`UploadImageAsync` validates extension only** | `ProductService.cs` | Checks file extension (`.jpg`, `.png`, etc.) but not actual file content. A PHP script renamed to `.jpg` passes validation. | Validate magic bytes (file signature) in addition to extension. |

#### Frontend — New Issues

| # | Severity | Issue | Location | Details | Fix |
|---|----------|-------|----------|---------|-----|
| F58 | **High** | ~~**Chat: incoming messages route to wrong conversation**~~ | ~~`chat-page.component.ts`~~ | ✅ **FIXED** — Added `customerId` field to `ChatMessageDto` on backend (populated in all 3 places: `ChatService.GetMessagesAsync`, `ChatService.SendMessageAsync`, `ChatBotService.SaveAndPushBotMessage`, `WhatsAppWebhookController`). Frontend `ChatMessageEvent` interface updated with `customerId`. Chat page subscriber now guards: `if (msg.customerId === this.selectedCustomerId)` — messages for other conversations are silently ignored. | ~~Add customerId guard~~ ✅ Done |
| F59 | **High** | ~~**Product form: unsaved-changes guard false-positive**~~ | ~~`product-form.component.ts`~~ | ✅ **FIXED** — `originalSnapshot` is now set to `JSON.stringify(this.productForm.value)` immediately after `initForm()`, before the branch. This means both new-product and edit-product modes start with the correct baseline. In edit mode, the snapshot is updated again inside the subscribe callback once the product data loads. Eliminates the window where `originalSnapshot = ''` didn't match the form's default values. | ~~Set snapshot after initForm~~ ✅ Done |
| F60 | **High** | ~~**Navbar logout: `signalR.stop()` runs before navigation check**~~ | ~~`navbar.component.ts`~~ | ✅ **FIXED** — Moved `signalR.stop()` inside the `.then(async navigated => { if (navigated) { await signalR.stop(); auth.clearSession(); } })` block. If the `unsavedChangesGuard` blocks navigation (user clicks "Stay"), SignalR stays connected. The `stop()` method now returns a `Promise<void>` (see F71) and is awaited before clearing the session. | ~~Move stop() after navigation~~ ✅ Done |
| F61 | **Medium** | **Customers: "Select All" selects across all paginator pages** | `customers.component.ts` `toggleSelectAll()` | Client-side pagination shows 25 rows, but master checkbox toggles ALL customers in the array (could be hundreds). Broadcast goes to all, not just visible page. Dangerously misleading. | Track selection per-page, or show "All X selected across all pages" warning. |
| F62 | **Medium** | **Customers: bulk import has no phone validation** | `customers.component.ts` bulk import | Single-add validates with `Validators.pattern(/^\d{10,15}$/)`, but bulk import has zero validation. Also `.split(',')` breaks if name contains commas. | Validate each phone, use `line.split(',', 2)`. |
| F63 | **Medium** | **Dashboard/Products: API error shows "empty" state** | `dashboard.component.ts`, `product-list.component.ts` | On API failure, dashboard shows blank screen, product list shows "No products found. Add your first product!" — misleading when the real problem is a network error. | Add `errorOccurred` flag and show error/retry UI. |
| F64 | **Medium** | **Chat: loading older messages causes scroll position jump** | `chat-page.component.ts` `loadMessages()` | Older messages prepended to array. Angular re-renders, shifting viewport. User loses reading position. | Save `scrollHeight` before prepend, restore: `scrollTop = newScrollHeight - oldScrollHeight`. |
| F65 | **Medium** | ~~**Broadcast: polling conflict on second broadcast**~~ | ~~`broadcast.component.ts`~~ | ✅ **FIXED** — Replaced single `pollingInterval` with `Map<number, ReturnType<typeof setInterval>>` (`pollingIntervals`). Each broadcast gets its own interval tracked by `broadcastId`. When a broadcast completes, only its own interval is cleared. `sending` flag is true while any poll is active (`pollingIntervals.size > 0`). `ngOnDestroy` clears all intervals in the map. Duplicate polls for the same broadcast are prevented by `has()` check. | ~~Track per-broadcast~~ ✅ Done |
| F66 | **Medium** | **Missing accessible labels on filter controls** | `orders.component.html`, `chat-page.component.html` | Status dropdown, search input, and message input lack `aria-label`. Screen readers only read placeholder or generic "edit text". | Add `aria-label` attributes on all interactive controls. |
| F67 | **Low** | **Broadcast result banner leaks between Quick/Template modes** | `broadcast.component.ts` | `resultMessage` and `resultType` are shared state. Success/error banner from one mode visible when switching to the other. | Clear `resultMessage = ''` when switching modes. |
| F68 | **Low** | **`getStatusButtonSeverity` is dead code** | `severity.utils.ts`, `orders.component.ts` | Returns `'primary' as TagSeverity` (invalid cast — not in union type). Template uses inline logic instead. Both the util function and component wrapper are unused. | Remove dead function and wrapper. |
| F69 | **Low** | **Product list: unreachable `emptymessage` template** | `product-list.component.html` | `<p-table>` only mounts when `products.length > 0`, so the `emptymessage` template inside can never render. | Remove the dead template. |
| F70 | **Low** | **Chat: `deleteConversation` shows no success toast** | `chat-page.component.ts` | Every other destructive action shows a confirmation toast, but conversation delete does not. Inconsistent UX. | Add `this.notification.success('Conversation deleted.')`. |
| F71 | **Low** | ~~**SignalR `stop()` doesn't await the Promise**~~ | ~~`signalr.service.ts`~~ | ✅ **FIXED** — `stop()` now returns `Promise<void>`. Saves the connection reference to a local variable, nulls `hubConnection` first (prevents new calls during shutdown), then returns `conn.stop()` which the caller can await. `ngOnDestroy` doesn't need to await (service is being destroyed anyway). Navbar logout awaits `stop()` before clearing session (see F60). | ~~Make async, await stop~~ ✅ Done |
| F72 | **Low** | ~~**Customers: `loadCounts()` silently swallows errors**~~ | ~~`customers.component.ts`~~ | ✅ **FIXED** — Added error handler that sets `subscriberCount` and `totalCount` to `null` on API failure. Template uses `!== null` check: shows "N/A" when counts are null instead of misleading zeros. Error interceptor still shows the toast for the underlying API failure. | ~~Show N/A on error~~ ✅ Done |

#### New Findings (Deep Re-analysis — Feb 26, 2026, Round 2)

8 additional issues found. Cross-referenced against all existing items — zero duplicates.

| # | Severity | Issue | Location | Details | Fix |
|---|----------|-------|----------|---------|-----|
| F73 | **High** | **Webhook error aborts entire batch** | `WhatsAppWebhookController.cs` L60-134 | The try/catch wraps the **entire** message processing loop. If one message in a batch throws, all subsequent messages in the same webhook payload are silently dropped. | Move try/catch **inside** the per-message `foreach` loop. |
| F74 | **High** | **Stock inflation on un-cancellation** | `OrderService.cs` L63-69 | Stock is restored when cancelling, but changing FROM `Cancelled` to another status never re-deducts. Creates phantom inventory. Combined with F31 (no transition validation), admin can `Cancelled → Pending` and inflate stock. | Either block un-cancellation (F31 state machine) or re-deduct stock on status change from Cancelled. |
| F75 | **Medium** | **`AmountInPaise` truncation risk** | `PaymentService.cs` L47 | `(int)(order.TotalAmount * 100)` truncates instead of rounding. `99.999m` → 9999 instead of 10000. | Use `(int)Math.Round(order.TotalAmount * 100)`. |
| F76 | **Medium** | **WhatsApp service fail-open on missing config** | `WhatsAppService.cs` L18-20 | `PhoneNumberId` and `AccessToken` use null-forgiving `!` on potentially empty config values. Empty `Bearer` token silently makes failing API calls instead of failing fast. | Validate config at startup; throw if required WhatsApp values are empty. |
| F77 | **Medium** | **No HTTPS enforcement/HSTS** | `Program.cs` L74 | App binds HTTP only. No `UseHttpsRedirection()` or HSTS headers. Railway provides TLS termination but direct HTTP is possible. | Add `app.UseHttpsRedirection()` and HSTS headers. |
| F78 | **Medium** | **Chat: stale messages on quick conversation switch** | `chat-page.component.ts` `selectConversation/loadMessages` | Setting `selectedCustomerId` and calling HTTP `loadMessages()` without cancelling previous in-flight request. Quick A→B switch can briefly show A's messages in B's panel until B's response arrives. | Use `switchMap` or check `selectedCustomerId` still matches in the subscribe callback. |
| F79 | **Medium** | **Chat: `loadConversations()` called on every message** | `chat-page.component.ts` `newChatMessage$` | No debounce/throttle on the SignalR event handler. In busy chat, N messages = N full conversation-list API calls in quick succession. | Add `debounceTime(1000)` to the subscription, or update conversation metadata locally. |
| F80 | **Medium** | ~~**SignalR: dead connection after reconnect exhaustion**~~ | ~~`signalr.service.ts` `onclose`~~ | ✅ **FIXED** — `onclose` callback now sets `this.hubConnection = null`, allowing `start()` to create a fresh connection after automatic reconnect is exhausted. Previously, the stale non-null reference caused `start()` to return immediately as a no-op. | ~~Null hubConnection in onclose~~ ✅ Done |
