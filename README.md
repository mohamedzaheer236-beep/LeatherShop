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
| **Controllers (thin)** | `AuthController.cs`, `ProductsController.cs`, `OrdersController.cs`, `CustomersController.cs`, `DashboardController.cs`, `BroadcastController.cs`, `PaymentController.cs`, `WhatsAppWebhookController.cs` | HTTP routing only — delegates all logic to service interfaces. Wraps responses in `ApiResponse<T>`. `[Authorize]` on all admin controllers; Auth/Payment/Webhook are public. |
| **Service Interfaces** | `Services/Interfaces/IProductService.cs`, `IOrderService.cs`, `ICustomerService.cs`, `IDashboardService.cs`, `IBroadcastService.cs`, `IPaymentService.cs`, `IWhatsAppService.cs`, `IChatBotService.cs` | Contracts for all business logic |
| **Service Implementations** | `Services/ProductService.cs`, `OrderService.cs`, `CustomerService.cs`, `DashboardService.cs`, `BroadcastService.cs`, `PaymentService.cs`, `WhatsAppService.cs`, `ChatBotService.cs` | All business logic lives here — DB queries, WhatsApp API calls, chatbot state machine |
| **Background Processing** | `Services/BroadcastBackgroundService.cs` | Hosted `BackgroundService` + `Channel<T>` producer/consumer queue — `BroadcastService` enqueues jobs, `BroadcastBackgroundService` dequeues and processes with `SemaphoreSlim(10)` concurrency. Saves progress every 50 messages. Graceful shutdown via `CancellationToken`. |
| **Entity Configurations** | `Data/Configurations/ProductConfiguration.cs`, `CustomerConfiguration.cs`, `CartItemConfiguration.cs`, `OrderConfiguration.cs`, `OrderItemConfiguration.cs`, `BroadcastMessageConfiguration.cs` | Fluent API: relationships (1:1, 1:N, M:1), indexes, unique constraints, delete behavior, seed data |
| **Split DTOs (validated)** | `DTOs/Product/`, `DTOs/Order/`, `DTOs/Customer/`, `DTOs/Dashboard/`, `DTOs/Broadcast/`, `DTOs/Payment/`, `DTOs/WhatsApp/` | Per-feature DTO files with `[Required]`, `[MaxLength]`, `[Range]`, `[Url]`, `[RegularExpression]` validation attributes |
| **DI Extensions** | `Extensions/ServiceCollectionExtensions.cs` | Grouped DI registration: `AddDatabase()`, `AddApplicationServices()`, `AddCorsPolicies()` |
| **Mapping Extensions** | `Extensions/MappingExtensions.cs` | `Product.ToDto()`, `Order.ToDto()`, `OrderItem.ToDto()` — shared entity-to-DTO mapping used by ProductService, OrderService, DashboardService |
| **Authentication** | `Controllers/AuthController.cs`, `Models/AdminUser.cs`, `DTOs/Auth/AuthDtos.cs`, `Data/Configurations/AdminUserConfiguration.cs` | JWT Bearer authentication — `POST /api/auth/login` validates credentials against `AdminUsers` table (BCrypt hash, case-sensitive). Returns JWT token (24h expiry). `[Authorize]` attribute on all admin controllers. Admin user auto-seeded on first startup. |
| **Config** | `appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json` | Environment-specific configuration files |
| **Data Models** | `Models/Product.cs`, `Customer.cs`, `CartItem.cs`, `Order.cs`, `OrderItem.cs`, `BroadcastMessage.cs`, `AdminUser.cs` | Entity classes with navigation properties |
| **Database** | `AppDbContext.cs` | EF Core DbContext — uses `ApplyConfigurationsFromAssembly()` for auto-discovering entity configs. 7 DbSets including AdminUsers. |

### Frontend Admin Panel (Angular 18) — `LeatherShopAdmin/`

**Architecture:** Feature-based module structure with per-feature models, services, components, and route files. Lazy-loaded routes for each feature. Shared components in `shared/`.

| Feature Module | Route | Key Files |
|----------------|-------|-----------|
| **Dashboard** | `/dashboard` (lazy) | `features/dashboard/` — `dashboard.service.ts`, `dashboard.model.ts`, `dashboard.routes.ts`, `components/dashboard/` |
| **Products** | `/products` (lazy) | `features/products/` — `product.service.ts`, `product.model.ts`, `products.routes.ts`, `components/product-list/`, `components/product-form/` |
| **Orders** | `/orders` (lazy) | `features/orders/` — `order.service.ts`, `order.model.ts`, `orders.routes.ts`, `components/orders/` |
| **Customers** | `/customers` (lazy) | `features/customers/` — `customer.service.ts`, `customer.model.ts`, `customers.routes.ts`, `components/customers/` |
| **Broadcast** | `/broadcast` (lazy) | `features/broadcast/` — `broadcast.service.ts`, `broadcast.model.ts`, `broadcast.routes.ts`, `components/broadcast/` |
| **Auth** | `/login` | `features/auth/components/login/` — animated login page with background video, JWT token storage, redirect to dashboard on success |
| **Core** | _(app-wide)_ | `core/interceptors/error.interceptor.ts` — HTTP error interceptor with toast notifications. `core/interceptors/auth.interceptor.ts` — attaches JWT Bearer token to all API requests. `core/guards/auth.guard.ts` — protects all admin routes (redirects to `/login` if no token). `core/services/auth.service.ts` — login, logout, token management, username extraction. |
| **Shared** | _(all pages)_ | `shared/components/navbar/`, `shared/components/toast/`, `shared/components/loading-spinner/`, `shared/services/notification.service.ts`, `shared/services/template-loader.service.ts`, `shared/utils/severity.utils.ts` |
| **Environments** | _(build-time)_ | `environments/environment.ts` (dev), `environments/environment.prod.ts` (prod) — API URL config |
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
│                    ┌──────▼──────────────────┐        │
│                    │  PostgreSQL (EF Core)   │        │
│                    │  Products, Customers,   │        │
│                    │  CartItems, Orders,     │        │
│                    │  OrderItems, Broadcasts │        │
│                    └────────────────────────┘         │
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
```

---

## Project Structure

```
LeatherShopAPI/                          # ── .NET 8 Web API ──
│
├── Program.cs                           # App entry point — clean, uses extension methods
│                                        #   - JWT Bearer authentication configuration
│                                        #   - Uses ExceptionHandlingMiddleware
│                                        #   - Auto-runs EF migrations on startup
│                                        #   - Seeds admin user (BCrypt hash) if none exists
│                                        #   - Enables Swagger in development
│
├── appsettings.json                     # Config: DB connection, WhatsApp creds, Razorpay keys, JWT settings
├── appsettings.Development.json         # Development overrides (log levels)
├── appsettings.Production.json          # Production template (placeholder secrets)
│
├── Extensions/
│   ├── ServiceCollectionExtensions.cs   # Grouped DI registration
│   └── MappingExtensions.cs             # Entity → DTO extension methods
│                                        #   Product.ToDto(), Order.ToDto(), OrderItem.ToDto()
│                                        #   Eliminates duplicate mapping across services
│                                        #   - AddDatabase() — PostgreSQL context
│                                        #   - AddApplicationServices() — all 8 services
│                                        #   - AddCorsPolicies() — CORS for Angular
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
│   │                                    #   IsSubscribed → has Orders, CartItems
│   ├── CartItem.cs                      # Id, CustomerId, ProductId, Quantity
│   │                                    #   (unique constraint: customer + product)
│   ├── Order.cs                         # Id, OrderNumber (unique), CustomerId,
│   │                                    #   TotalAmount, Status, PaymentId, IsPaid
│   │                                    # OrderItem: OrderId, ProductId, Qty, UnitPrice
│   ├── BroadcastMessage.cs              # Id, MessageTemplate, MessageBody,
│   │                                    #   TotalRecipients, SentCount, FailedCount
│   └── AdminUser.cs                     # Id, Username (unique), PasswordHash (BCrypt),
│                                        #   CreatedAt, LastLoginAt
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
│   ├── PaymentController.cs             # Public (customer-facing) — Injects IPaymentService
│   └── WhatsAppWebhookController.cs     # Public (Meta webhook) — Injects IChatBotService
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
│   │   └── IPaymentService.cs           # Payment page + verify
│   │
│   ├── WhatsAppService.cs               # Implements IWhatsAppService
│   ├── ChatBotService.cs                # Implements IChatBotService (state machine)
│   ├── ProductService.cs                # Implements IProductService
│   ├── OrderService.cs                  # Implements IOrderService
│   ├── CustomerService.cs               # Implements ICustomerService
│   ├── DashboardService.cs              # Implements IDashboardService
│   ├── BroadcastService.cs              # Implements IBroadcastService (enqueues to Channel)
│   ├── BroadcastBackgroundService.cs    # Hosted BackgroundService — reads from Channel<T>,
│   │                                    #   processes broadcasts with SemaphoreSlim(10)
│   │                                    #   concurrency, saves progress every 50 messages
│   └── PaymentService.cs                # Implements IPaymentService
│
├── Data/
│   ├── AppDbContext.cs                  # 7 DbSets, uses ApplyConfigurationsFromAssembly()
│   └── Configurations/                  # Fluent API entity configurations
│       ├── ProductConfiguration.cs      # Indexes on Category/Brand, seed data
│       ├── CustomerConfiguration.cs     # Unique PhoneNumber, 1:N → Orders, 1:N → CartItems
│       ├── CartItemConfiguration.cs     # Unique (CustomerId+ProductId), M:1 relationships
│       ├── OrderConfiguration.cs        # Unique OrderNumber, M:1 → Customer, 1:N → OrderItems
│       ├── OrderItemConfiguration.cs    # M:1 → Order, M:1 → Product (Restrict delete)
│       ├── BroadcastMessageConfiguration.cs
│       └── AdminUserConfiguration.cs    # Unique Username, max lengths
│
├── DTOs/                                # Split per feature, with validation attributes
│   ├── Auth/AuthDtos.cs                 # LoginRequest (Username, Password), LoginResponse (Token, Expiry, Username)
│   ├── Product/ProductDtos.cs           # [Required], [MaxLength], [Range], [Url]
│   ├── Order/OrderDtos.cs               # OrderDto, OrderItemDto
│   ├── Customer/CustomerDtos.cs         # [Required], [RegularExpression] phone, [MinLength]
│   ├── Dashboard/DashboardDtos.cs       # DashboardDto
│   ├── Broadcast/BroadcastDtos.cs       # [Required] template, [Url] image
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
│   │   ├── environment.ts               # Dev config (apiUrl: localhost:5000)
│   │   └── environment.prod.ts          # Prod config (apiUrl: production URL)
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
│       │       └── auth.service.ts      # login(), logout(), isLoggedIn(), getUsername()
│       │                                #   JWT token management via localStorage
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
```

---

## Developer Setup Guide

### Repository Access

This repo is **private**. Only the owner and added collaborators can clone/push.

```bash
git clone https://github.com/mohamedzaheer236-beep/LeatherShop.git
cd LeatherShop
```

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

### Step 2: Configure the Backend

Edit `LeatherShopAPI/appsettings.json` and set your PostgreSQL password:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=LeatherShopDB;Username=postgres;Password=YOUR_POSTGRES_PASSWORD"
  },
  "WhatsApp": {
    "PhoneNumberId": "YOUR_PHONE_NUMBER_ID",
    "AccessToken": "YOUR_WHATSAPP_ACCESS_TOKEN",
    "VerifyToken": "ANY_CUSTOM_STRING_YOU_CHOOSE",
    "ApiVersion": "v18.0"
  },
  "Razorpay": {
    "KeyId": "rzp_test_xxxxx",
    "KeySecret": "YOUR_RAZORPAY_SECRET"
  },
  "App": {
    "BaseUrl": "https://leathershop-production.up.railway.app"
  }
}
```

> **Note:** You can start with just the database password configured. WhatsApp and Razorpay can be set up later. The admin panel and API will work without them — only the chatbot and payments need those keys.

### Step 3: Run the Backend API

```bash
cd LeatherShopAPI
dotnet run
```

- API starts on **http://localhost:5000**
- Swagger UI at **http://localhost:5000/swagger**
- On first run, it auto-creates the database and seeds 6 sample leather products

### Step 4: Run the Angular Admin Panel

```bash
cd LeatherShopAdmin
npm install        # first time only
npx ng serve
```

- Admin panel opens at **http://localhost:4200**
- It calls the API at `http://localhost:5000` (configured in `src/environments/environment.ts`)

### Step 5: Verify Everything Works

1. Open **http://localhost:5000/swagger** — you should see all API endpoints
2. Open **http://localhost:4200** — you should be redirected to the login page
3. Log in with the admin credentials — dashboard should load with 6 products, 0 orders
4. Go to **Products** page — you should see the 6 seeded products

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
| GET | `/api/customers` | List all customers. Query params: `?subscribedOnly=true` |
| GET | `/api/customers/count` | Get subscriber count and total count |

### Dashboard
| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/dashboard` | Dashboard stats + 10 recent orders |

### Broadcast
| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/broadcast/send` | Send template message to all subscribers |
| GET | `/api/broadcast/history` | Last 20 broadcast records |

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
│ Price       │       │ CreatedAt    │       │ FailedCount  │
│ StockQty    │       │ UpdatedAt    │       │ SentAt       │
│ ImageUrl    │       └──────┬───────┘       └──────────────┘
│ IsActive    │              │
│ CreatedAt   │              │ 1:N               ┌──────────────┐
│ UpdatedAt   │              │                   │  AdminUsers  │
└──────┬──────┘       ┌──────▼───────┐           ├──────────────┤
       │              │  CartItems   │           │ Id (PK)      │
       │              ├──────────────┤           │ Username  ◄──│─unique
       │   ┌──────────│ Id (PK)      │           │ PasswordHash │
       │   │          │ CustomerId(FK)│          │ CreatedAt    │
       │   │          │ ProductId(FK)│◄─unique   │ LastLoginAt  │
       │   │          │ Quantity     │(Cust+Prod)└──────────────┘
       │   │          │ AddedAt      │
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
                      │ ShippingAddr │
                      │ CreatedAt    │
                      │ UpdatedAt    │
                      └──────────────┘

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
| **Product Image in WhatsApp** | Chatbot sends text-only product details. Could send image messages using the WhatsApp media API. |
| **Customer Address Collection** | Checkout uses the stored address (usually empty). No chatbot flow to ask for shipping address. |
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
| C2 | **Secrets Committed to Source** | `appsettings.json` | Live WhatsApp access token + DB password are in plaintext in the repo. Must use User Secrets for dev, environment variables or Azure Key Vault for production. |
| C3 | ~~**Razorpay Signature Verification TODO'd Out**~~ | ~~`PaymentService.cs`~~ | **FIXED** — `VerifyPaymentAsync` now computes HMAC-SHA256 signature from `RazorpayOrderId|PaymentId` using the `Razorpay:KeySecret` config value. When `KeySecret` is configured, verification is **mandatory** — missing signature or mismatch rejects the payment. When `KeySecret` is not configured (dev mode), logs a warning and allows the payment. `PaymentVerifyDto` updated with `RazorpayOrderId` field. Payment page JS passes `razorpay_order_id` in the verify request. |
| C4 | **WhatsApp Webhook Signature Not Validated** | `WhatsAppWebhookController.cs` | Meta sends `X-Hub-Signature-256` on every POST. The controller never checks it. Attackers can POST fabricated payloads to trigger chatbot flows and create fake orders. |
| C5 | ~~**XSS in Payment Page**~~ | ~~`PaymentController.cs`~~ | **FIXED** — All user-controlled values (`OrderNumber`, `CustomerPhone`, `ProductName`) are HTML-encoded with `WebUtility.HtmlEncode()` into safe local variables before interpolation into the payment HTML page. Numeric values (`TotalAmount`, `Quantity`, etc.) are strongly-typed decimals/ints and don't need encoding. |
| | C6 | ~~**DbContext Thread-Safety Bug**~~ | ~~`BroadcastBackgroundService.cs`~~ | **FIXED** — `ProcessBroadcastAsync` no longer shares a single `DbContext` across concurrent tasks. Each concurrent task creates its own `IServiceScope` (at most 10 alive at once via `SemaphoreSlim`). `SaveProgressAsync` uses a dedicated scope with `ExecuteUpdateAsync` (stateless SQL `UPDATE`, no entity tracking). The initial broadcast existence check uses a short-lived scope that is disposed before concurrency begins. No `DbContext` instance is ever accessed from multiple threads. | |

### 🟠 HIGH — Data Integrity / Bugs

| # | Issue | Location | Details |
|---|-------|----------|---------|
| H1 | **Race Condition: Overselling During Checkout** | `ChatBotService.cs` | Stock checked with `if (product.StockQuantity < qty)` then decremented in same method. Two concurrent checkouts can both pass and oversell. Fix: use optimistic concurrency (`RowVersion`) or `UPDATE WHERE StockQuantity >= @qty`. |
| H2 | ~~**Phone Format Mismatch → Duplicate Customers**~~ | ~~`CustomerService.cs` vs `ChatBotService.cs`~~ | **FIXED** — Created `PhoneNumberHelper.Normalize()` static helper that strips `+`, spaces, dashes, parentheses. Applied to all phone number entry points: `ChatBotService.ProcessMessage()` (normalizes `from` before lookup/create), `CustomerService.CreateAsync()` (normalizes input), `CustomerService.BulkImportAsync()` (normalizes each phone), `BroadcastService.SendBroadcastAsync()` (normalizes DTO phone numbers). All phone numbers stored without `+` prefix (e.g., `919876543210`) matching WhatsApp API format. |
| H3 | **No HTTPS Enforcement** | `Program.cs`, `launchSettings.json` | Payment page with Razorpay integration served over HTTP. No `app.UseHttpsRedirection()`. |
| H4 | ~~**Stock Not Restored on Order Cancellation**~~ | ~~`OrderService.cs`~~ | **FIXED** — `UpdateStatusAsync` now loads `OrderItems` with `Products` via `.Include()`. When status changes to `Cancelled` (and wasn’t already cancelled), restores `StockQuantity` for each order item. Prevents double-restore by checking previous status. |
| H5 | ~~**Description MaxLength Mismatch**~~ | ~~`ProductDtos.cs` vs `Product.cs`~~ | **FIXED** — Aligned `Product.cs` `[MaxLength]` from 1000 to 2000 to match DTO. Migration `UpdateProductDescriptionLength` created. |
| H6 | **Production API URL is a Placeholder** | `environment.prod.ts` | Points to `your-production-domain.com`. Production builds will fail. |
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
| 22 | **Error handlers** on all `subscribe()` calls with user-facing notifications and state rollback |

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
| `App__BaseUrl` | `https://leathershop-production.up.railway.app` |
| `FRONTEND_URL` | Vercel frontend URL (for CORS) |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `PORT` | Auto-set by Railway |

**Key deployment changes made:**
- `Program.cs` — reads `PORT` env variable for Railway, Swagger enabled in all environments (used as health check)
- `ServiceCollectionExtensions.cs` — `AddDatabase()` parses Railway `DATABASE_URL` URI format, `AddCorsPolicies()` reads `FRONTEND_URL` env var for production CORS
- `appsettings.Production.json` — placeholder values (`WILL_BE_SET_BY_RAILWAY_ENV_VAR`), actual secrets set via Railway environment variables

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
- [x] Database migration runs automatically on first startup
- [x] Health check configured (`/swagger/index.html` with 300s timeout)
- [x] Auto-restart on failure (max 10 retries)
- [x] Frontend deployed to Vercel with auto-deploy from GitHub
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
