# Leather Shop — WhatsApp Business Ordering System

A complete WhatsApp Business ordering system for a leather goods seller. Customers browse products, add to cart, and pay — all inside WhatsApp. The shop owner manages everything from an Angular admin panel.

**Tech Stack:** Angular 18 · .NET 8 Web API · Entity Framework Core · PostgreSQL · WhatsApp Cloud API · Razorpay

---

## Table of Contents

1. [What Has Been Built](#what-has-been-built)
2. [How It Works — System Architecture](#how-it-works--system-architecture)
3. [Customer WhatsApp Flow](#customer-whatsapp-flow)
4. [Admin Panel Flow](#admin-panel-flow)
5. [Project Structure](#project-structure)
6. [Developer Setup Guide](#developer-setup-guide)
7. [External Services Setup (WhatsApp, Razorpay, ngrok)](#external-services-setup)
8. [API Endpoints Reference](#api-endpoints-reference)
9. [Database Schema](#database-schema)
10. [What Is NOT Yet Implemented](#what-is-not-yet-implemented)
11. [Deployment Guide (Pending)](#deployment-guide-pending)

---

## What Has Been Built

### Backend API (.NET 8) — `LeatherShopAPI/`

**Architecture:** Interface → Service (business logic) → Controller (thin, HTTP only). Entity configurations via Fluent API. DI registration with `AddScoped`/`AddHttpClient`.

| Layer | File(s) | What It Does |
|-------|---------|--------------|
| **Middleware** | `Middleware/ExceptionHandlingMiddleware.cs` | Global exception handling — catches all unhandled exceptions, logs them, returns consistent `ApiResponse` JSON. Maps exception types to HTTP status codes (404, 400, 409, 401, 500). Prevents stack trace leaks. |
| **API Response Model** | `Models/ApiResponse.cs` | Unified response envelope `ApiResponse<T>` with `success`, `message`, `data`, `errors` fields. Generic and non-generic versions. All controllers return this shape. |
| **Controllers (thin)** | `ProductsController.cs`, `OrdersController.cs`, `CustomersController.cs`, `DashboardController.cs`, `BroadcastController.cs`, `PaymentController.cs`, `WhatsAppWebhookController.cs` | HTTP routing only — delegates all logic to service interfaces. Wraps responses in `ApiResponse<T>`. |
| **Service Interfaces** | `Services/Interfaces/IProductService.cs`, `IOrderService.cs`, `ICustomerService.cs`, `IDashboardService.cs`, `IBroadcastService.cs`, `IPaymentService.cs`, `IWhatsAppService.cs`, `IChatBotService.cs` | Contracts for all business logic |
| **Service Implementations** | `Services/ProductService.cs`, `OrderService.cs`, `CustomerService.cs`, `DashboardService.cs`, `BroadcastService.cs`, `PaymentService.cs`, `WhatsAppService.cs`, `ChatBotService.cs` | All business logic lives here — DB queries, WhatsApp API calls, chatbot state machine |
| **Background Processing** | `Services/BroadcastBackgroundService.cs` | Hosted `BackgroundService` + `Channel<T>` producer/consumer queue — `BroadcastService` enqueues jobs, `BroadcastBackgroundService` dequeues and processes with `SemaphoreSlim(10)` concurrency. Saves progress every 50 messages. Graceful shutdown via `CancellationToken`. |
| **Entity Configurations** | `Data/Configurations/ProductConfiguration.cs`, `CustomerConfiguration.cs`, `CartItemConfiguration.cs`, `OrderConfiguration.cs`, `OrderItemConfiguration.cs`, `BroadcastMessageConfiguration.cs` | Fluent API: relationships (1:1, 1:N, M:1), indexes, unique constraints, delete behavior, seed data |
| **Split DTOs (validated)** | `DTOs/Product/`, `DTOs/Order/`, `DTOs/Customer/`, `DTOs/Dashboard/`, `DTOs/Broadcast/`, `DTOs/Payment/`, `DTOs/WhatsApp/` | Per-feature DTO files with `[Required]`, `[MaxLength]`, `[Range]`, `[Url]`, `[RegularExpression]` validation attributes |
| **DI Extensions** | `Extensions/ServiceCollectionExtensions.cs` | Grouped DI registration: `AddDatabase()`, `AddApplicationServices()`, `AddCorsPolicies()` |
| **Config** | `appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json` | Environment-specific configuration files |
| **Data Models** | `Models/Product.cs`, `Customer.cs`, `CartItem.cs`, `Order.cs`, `OrderItem.cs`, `BroadcastMessage.cs` | Entity classes with navigation properties |
| **Database** | `AppDbContext.cs` | EF Core DbContext — uses `ApplyConfigurationsFromAssembly()` for auto-discovering entity configs |

### Frontend Admin Panel (Angular 18) — `LeatherShopAdmin/`

**Architecture:** Feature-based module structure with per-feature models, services, components, and route files. Lazy-loaded routes for each feature. Shared components in `shared/`.

| Feature Module | Route | Key Files |
|----------------|-------|-----------|
| **Dashboard** | `/dashboard` (lazy) | `features/dashboard/` — `dashboard.service.ts`, `dashboard.model.ts`, `dashboard.routes.ts`, `components/dashboard/` |
| **Products** | `/products` (lazy) | `features/products/` — `product.service.ts`, `product.model.ts`, `products.routes.ts`, `components/product-list/`, `components/product-form/` |
| **Orders** | `/orders` (lazy) | `features/orders/` — `order.service.ts`, `order.model.ts`, `orders.routes.ts`, `components/orders/` |
| **Customers** | `/customers` (lazy) | `features/customers/` — `customer.service.ts`, `customer.model.ts`, `customers.routes.ts`, `components/customers/` |
| **Broadcast** | `/broadcast` (lazy) | `features/broadcast/` — `broadcast.service.ts`, `broadcast.model.ts`, `broadcast.routes.ts`, `components/broadcast/` |
| **Core** | _(app-wide)_ | `core/interceptors/error.interceptor.ts` — HTTP error interceptor with toast notifications |
| **Shared** | _(all pages)_ | `shared/components/navbar/`, `shared/components/toast/`, `shared/components/loading-spinner/`, `shared/services/notification.service.ts` |
| **Environments** | _(build-time)_ | `environments/environment.ts` (dev), `environments/environment.prod.ts` (prod) — API URL config |
| **App Shell** | — | `app.routes.ts` (lazy loading via `loadChildren`), `app.config.ts` (interceptors), `app.component.ts` (toast + navbar + outlet) |

---

## How It Works — System Architecture

```
┌─────────────────────┐         ┌──────────────────────┐
│   CUSTOMER           │         │   SHOP OWNER          │
│   (WhatsApp)         │         │   (Browser)           │
└────────┬────────────┘         └──────────┬───────────┘
         │                                  │
         │ WhatsApp Messages                │ HTTP (localhost:4200)
         ▼                                  ▼
┌─────────────────────┐         ┌──────────────────────┐
│  Meta WhatsApp       │         │  Angular 18           │
│  Cloud API           │         │  Admin Panel          │
│  (graph.facebook.com)│         │  (LeatherShopAdmin)   │
└────────┬────────────┘         └──────────┬───────────┘
         │                                  │
         │ Webhook POST                     │ REST API calls
         │ (ngrok tunnel)                   │ (http://localhost:5000)
         ▼                                  ▼
┌──────────────────────────────────────────────────────┐
│              .NET 8 Web API                           │
│              (LeatherShopAPI - localhost:5000)         │
│                                                       │
│  ┌──────────────┐  ┌─────────────┐  ┌─────────────┐  │
│  │ Webhook       │  │ ChatBot     │  │ Admin       │  │
│  │ Controller    │──│ Service     │  │ Controllers │  │
│  └──────────────┘  └──────┬──────┘  └──────┬──────┘  │
│                           │                 │         │
│                    ┌──────▼──────┐          │         │
│                    │ WhatsApp    │◄─────────┘         │
│                    │ Service     │ (order status      │
│                    │ (sends msgs)│  notifications)    │
│                    └─────────────┘                    │
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
│                                        #   - Uses ExceptionHandlingMiddleware
│                                        #   - Auto-runs EF migrations on startup
│                                        #   - Enables Swagger in development
│
├── appsettings.json                     # Config: DB connection, WhatsApp creds, Razorpay keys
├── appsettings.Development.json         # Development overrides (log levels)
├── appsettings.Production.json          # Production template (placeholder secrets)
│
├── Extensions/
│   └── ServiceCollectionExtensions.cs   # Grouped DI registration
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
│   └── BroadcastMessage.cs              # Id, MessageTemplate, MessageBody,
│                                        #   TotalRecipients, SentCount, FailedCount
│
├── Controllers/                         # THIN — wraps responses in ApiResponse<T>
│   ├── ProductsController.cs            # Injects IProductService
│   ├── OrdersController.cs              # Injects IOrderService
│   ├── CustomersController.cs           # Injects ICustomerService
│   ├── DashboardController.cs           # Injects IDashboardService
│   ├── BroadcastController.cs           # Injects IBroadcastService
│   ├── PaymentController.cs             # Injects IPaymentService
│   └── WhatsAppWebhookController.cs     # Injects IChatBotService
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
│   ├── AppDbContext.cs                  # 6 DbSets, uses ApplyConfigurationsFromAssembly()
│   └── Configurations/                  # Fluent API entity configurations
│       ├── ProductConfiguration.cs      # Indexes on Category/Brand, seed data
│       ├── CustomerConfiguration.cs     # Unique PhoneNumber, 1:N → Orders, 1:N → CartItems
│       ├── CartItemConfiguration.cs     # Unique (CustomerId+ProductId), M:1 relationships
│       ├── OrderConfiguration.cs        # Unique OrderNumber, M:1 → Customer, 1:N → OrderItems
│       ├── OrderItemConfiguration.cs    # M:1 → Order, M:1 → Product (Restrict delete)
│       └── BroadcastMessageConfiguration.cs
│
├── DTOs/                                # Split per feature, with validation attributes
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
│       │   └── interceptors/
│       │       └── error.interceptor.ts # HTTP error interceptor — catches all API
│       │                                #   errors, shows toast notifications
│       │
│       ├── shared/
│       │   ├── services/
│       │   │   └── notification.service.ts  # Centralized toast notification service
│       │   └── components/
│       │       ├── navbar/              # Navigation bar (ts, html, scss)
│       │       ├── toast/               # Toast notification component (auto-dismiss)
│       │       └── loading-spinner/     # Reusable loading spinner component
│       │
│       └── features/                    # Feature-based modules
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
    "BaseUrl": "https://your-ngrok-url.ngrok-free.app"
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
2. Open **http://localhost:4200** — dashboard should load with 6 products, 0 orders
3. Go to **Products** page — you should see the 6 seeded products

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
3. Copy **Temporary Access Token** → paste in `WhatsApp:AccessToken`
4. For production: generate a **Permanent Token** via System Users in Business Settings

#### 3. Set Up ngrok (Required for Webhook)

WhatsApp needs to reach your local API via a public URL. Install ngrok:

```bash
# Download from https://ngrok.com/download
# Or install via Chocolatey (Windows):
choco install ngrok

# Authenticate (one-time, get token from https://dashboard.ngrok.com):
ngrok config add-authtoken YOUR_NGROK_AUTH_TOKEN

# Start tunnel:
ngrok http 5000
```

This gives you a public URL like `https://abc123.ngrok-free.app`. Update `App:BaseUrl` in `appsettings.json` with this URL.

#### 4. Configure Webhook in Meta Console
1. Meta Developer Console → WhatsApp → Configuration → **Webhook**
2. **Callback URL**: `https://YOUR_NGROK_URL/api/whatsapp/webhook`
3. **Verify Token**: same value as `WhatsApp:VerifyToken` in your appsettings.json
4. Subscribe to: **`messages`**

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
│ CreatedAt   │              │ 1:N
│ UpdatedAt   │              │
└──────┬──────┘       ┌──────▼───────┐
       │              │  CartItems   │
       │              ├──────────────┤
       │   ┌──────────│ Id (PK)      │
       │   │          │ CustomerId(FK)│
       │   │          │ ProductId(FK)│◄─unique(CustomerId+ProductId)
       │   │          │ Quantity     │
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
| **Authentication / Authorization** | No login system. Admin panel is open to anyone. Need JWT or session-based auth for admin APIs. |
| **Image Upload** | Products only store an image URL string. No actual file upload — need cloud storage (S3, Azure Blob, Cloudinary). |
| **Razorpay Signature Verification** | Payment verify endpoint has a TODO — does not validate HMAC SHA256 signature. Unsafe for production. |
| **Broadcast DB Update** | ~~Fixed~~ — see Recently Implemented below. |
| **Logging to File/Service** | Uses default console logging only. Need Serilog or similar for production. |
| **Rate Limiting** | No API rate limiting on admin endpoints. |
| **Pagination** | Product and order lists return all records. Need pagination for large datasets. |
| **Product Image in WhatsApp** | Chatbot sends text-only product details. Could send image messages using the WhatsApp media API. |
| **Multi-quantity in Cart** | ~~Fixed~~ — see Recently Implemented below. |
| **Customer Address Collection** | Checkout uses the stored address (usually empty). No chatbot flow to ask for shipping address. |
| **Order Cancellation by Customer** | No WhatsApp flow for customers to cancel orders. |
| **Webhook Security** | No signature verification on incoming WhatsApp webhook requests (should validate `X-Hub-Signature-256`). |
| **HTTPS in Production** | API runs on HTTP. Needs reverse proxy (nginx) with SSL for production. |
| **Permanent WhatsApp Access Token** | Currently using a **temporary test token** from Meta dashboard that **expires every 24 hours**. Need to create a System User in Meta Business Suite → generate a permanent token with `whatsapp_business_messaging` and `whatsapp_business_management` permissions. |
| **WhatsApp Message Templates** | Broadcast messages currently send plain text — this **only works within 24 hours** of the customer's last message. For production, need to: (1) Create an approved Message Template in Meta WhatsApp Manager with `{{1}}` (customer name) and `{{2}}` (custom message) placeholders, (2) Update `WhatsAppService` to send template-format messages for broadcasts and order status updates. Category: Marketing for broadcasts, Utility for order updates. |
| **Production Deployment** | Currently runs on localhost only. Need to deploy API + DB + Angular to cloud for 24/7 WhatsApp webhook availability. See [Deployment Guide](#deployment-guide-pending) below. |

---

## Deployment Guide (Pending)

The API **must run 24/7** for WhatsApp to work — Meta sends webhook events whenever a customer messages, and if the API is offline, those messages are lost after retry expiry. Currently everything runs on localhost which stops when the PC is off.

### Recommended Architecture

```
┌──────────────┐     ┌──────────────────┐     ┌────────────────────┐
│  Angular SPA │     │  .NET 8 Web API  │     │  PostgreSQL DB     │
│  (Static)    │     │  (Always Running)│     │  (Managed)         │
│              │     │                  │     │                    │
│  Vercel /    │────▶│  Railway /       │────▶│  Railway Postgres /│
│  Netlify /   │     │  Azure App Svc / │     │  Supabase /        │
│  Azure SWA   │     │  DigitalOcean /  │     │  Azure PostgreSQL /│
│              │     │  AWS App Runner  │     │  AWS RDS           │
└──────────────┘     └──────────────────┘     └────────────────────┘
                            │
                     Meta WhatsApp Cloud API
                     (webhook URL = your API)
```

### Step-by-Step Deployment Plan

#### 1. Database — Managed PostgreSQL

| Option | Free Tier | Notes |
|--------|-----------|-------|
| **Railway** | 500 hrs/month, 1 GB | Easiest — same platform as API |
| **Supabase** | 500 MB, 2 projects | Has dashboard UI, REST API |
| **Neon** | 0.5 GB, auto-suspend | Serverless Postgres, great free tier |
| **Azure Database for PostgreSQL** | Flexible Server B1ms | 750 hrs free (12 months) |

**Steps:**
1. Create a managed PostgreSQL instance on chosen provider
2. Get the connection string (host, port, database, user, password)
3. Update `appsettings.Production.json` with the production connection string
4. EF Core auto-migrates on startup (`context.Database.Migrate()` in `Program.cs`)

#### 2. Backend API — .NET 8 (Must Be Always Running)

| Option | Free Tier | Notes |
|--------|-----------|-------|
| **Railway** | 500 hrs/month ($5 credit) | Deploy from GitHub, auto-builds .NET |
| **Azure App Service** | F1 free tier (60 min/day CPU) | Best for .NET, but free tier sleeps |
| **DigitalOcean App Platform** | $5/mo Starter | No free tier, but very reliable |
| **AWS App Runner** | ~750 hrs free (12 months) | Auto-scaling, container-based |
| **Render** | Free tier (spins down after 15 min) | Not ideal — webhook misses during cold start |

> **Important:** Free tiers that "sleep" (Render, Azure F1) will miss WhatsApp webhooks. For production, use a paid tier or Railway (stays awake within free credits).

**Steps:**
1. Add a `Dockerfile` to `LeatherShopAPI/`:
   ```dockerfile
   FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
   WORKDIR /app
   EXPOSE 8080

   FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
   WORKDIR /src
   COPY ["LeatherShopAPI.csproj", "."]
   RUN dotnet restore
   COPY . .
   RUN dotnet publish -c Release -o /app/publish

   FROM base AS final
   WORKDIR /app
   COPY --from=build /app/publish .
   ENV ASPNETCORE_URLS=http://+:8080
   ENTRYPOINT ["dotnet", "LeatherShopAPI.dll"]
   ```
2. Set environment variables on the hosting platform:
   - `ConnectionStrings__DefaultConnection` = production PostgreSQL connection string
   - `WhatsApp__PhoneNumberId` = your Meta phone number ID
   - `WhatsApp__AccessToken` = your Meta access token
   - `WhatsApp__VerifyToken` = your webhook verify token
   - `Razorpay__KeyId` = your Razorpay key
   - `Razorpay__KeySecret` = your Razorpay secret
   - `ASPNETCORE_ENVIRONMENT` = `Production`
3. Deploy from GitHub (most platforms auto-detect Dockerfile)
4. Note the deployed API URL (e.g., `https://leathershop-api.up.railway.app`)

#### 3. Frontend — Angular Static Site

| Option | Free Tier | Notes |
|--------|-----------|-------|
| **Vercel** | Unlimited static sites | Best DX, auto-deploys from GitHub |
| **Netlify** | 100 GB bandwidth | Great for SPAs with redirect rules |
| **Azure Static Web Apps** | Free tier | Integrated with Azure |
| **GitHub Pages** | Unlimited | Manual build step needed |

**Steps:**
1. Update `environment.prod.ts` → set `apiUrl` to the deployed API URL:
   ```typescript
   export const environment = {
     production: true,
     apiUrl: 'https://your-api-url.railway.app'
   };
   ```
2. Build for production:
   ```bash
   cd LeatherShopAdmin
   ng build --configuration production
   ```
3. Deploy the `dist/leather-shop-admin/browser/` folder to chosen hosting
4. Configure SPA redirect rules (all routes → `index.html`)

#### 4. WhatsApp Webhook — Update Meta Developer Console

1. Go to [Meta for Developers](https://developers.facebook.com/) → Your App → WhatsApp → Configuration
2. Change the **Callback URL** from `ngrok` to your deployed API:
   ```
   https://your-api-url.railway.app/api/whatsapp/webhook
   ```
3. Keep the same **Verify token** as your environment variable
4. Test by sending a WhatsApp message — Meta should hit your deployed API

#### 5. Post-Deployment Checklist

- [ ] WhatsApp webhook URL updated to production API URL
- [ ] All environment variables set (DB connection, WhatsApp tokens, Razorpay keys)
- [ ] CORS updated in API for production Angular URL
- [ ] HTTPS working (most platforms provide it automatically)
- [ ] Database migration ran successfully on first startup
- [ ] Test WhatsApp message flow end-to-end
- [ ] Test admin panel CRUD operations
- [ ] Test payment flow with Razorpay
- [ ] Monitor logs for errors (check platform's log viewer)
- [ ] Set up health check endpoint for uptime monitoring

### Estimated Cost (Budget Option)

| Component | Provider | Cost |
|-----------|----------|------|
| Angular SPA | Vercel | **Free** |
| .NET 8 API | Railway | **Free** (500 hrs/month) or **$5/month** |
| PostgreSQL | Railway / Supabase | **Free** (within limits) |
| Domain (optional) | Namecheap / GoDaddy | ~$10/year |
| **Total** | | **$0–$5/month** |

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
