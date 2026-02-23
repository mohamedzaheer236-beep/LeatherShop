# WhatsApp Business Setup — Connect Real Business Account

Guide to connect a real WhatsApp Business account (with blue tick, permanent token, unlimited users) to the Leather Shop API.

---

## Why Do This?

| Test Mode (Current) | Production (After Setup) |
|---------------------|--------------------------|
| Token expires every 24h | **Permanent token** (never expires) |
| Max 5 pre-registered test numbers | **Unlimited users** (any WhatsApp user) |
| Meta's test number (+1 555 145 5051) | **Real business number** with blue tick ✓ |
| No business name shown | **"LeatherShop"** (or business name) displayed |

---

## Prerequisites

- The business owner has a **Meta Verified WhatsApp Business** account (blue tick)
- You have an existing Meta Developer App (Leather Shop) at [developers.facebook.com](https://developers.facebook.com/)
- Your Leather Shop API is running (localhost or deployed)

---

## Step 1: Business Owner Adds You as Admin

**The business owner does this:**

1. Open [business.facebook.com](https://business.facebook.com/)
2. Click **Settings** (gear icon) → **Business Settings**
3. Left sidebar → **People**
4. Click **Add People** → enter your email (e.g., `zaheertn@gmail.com`)
5. Role: **Admin**
6. Click **Next** → assign his **WhatsApp Business Account** asset → **Full Control**
7. Click **Send Invitation**

**You do this:**

8. Check your email → **accept the invitation**

---

## Step 2: Add His Phone Number to Your Meta App

1. Go to [developers.facebook.com](https://developers.facebook.com/) → **My Apps** → your **Leather Shop** app
2. Left sidebar → **WhatsApp** → **API Setup**
3. In **Step 1: Select phone numbers** → click the **"From"** dropdown
4. Click **Add phone number**
5. Enter the business owner's phone number (the one with the blue tick)
6. Choose verification method → **SMS** or **Voice Call**
7. The business owner will receive an OTP on his phone → he tells you the code
8. Enter the OTP → phone number is now linked to your app

---

## Step 3: Get the New Credentials

Still on the **API Setup** page:

1. Make sure the **"From"** dropdown shows the business owner's phone number
2. Note down the **Phone Number ID** (shown below the dropdown, e.g., `91XXXXXXXXXX`)
3. Note down the **WhatsApp Business Account ID** (also shown on the page)
4. Click **Generate access token** → copy the token (this is temporary — Step 4 creates a permanent one)

---

## Step 4: Create a Permanent Token (Never Expires)

1. Go to [business.facebook.com](https://business.facebook.com/) → switch to the **business owner's business** (top-left dropdown)
2. **Settings** → **Business Settings** → left sidebar → **Users** → **System Users**
3. Click **Add**:
   - Name: `LeatherShopBot`
   - Role: **Admin**
   - Click **Create System User**
4. Click on `LeatherShopBot` → **Add Assets**:
   - Select **Apps** → your **Leather Shop** app → toggle **Full Control** → **Save**
   - Select **WhatsApp Accounts** → the business owner's WhatsApp account → toggle **Full Control** → **Save**
5. Click **Generate New Token**
6. Select your **Leather Shop** app
7. Check these permissions:
   - ✅ `whatsapp_business_messaging`
   - ✅ `whatsapp_business_management`
8. Click **Generate Token**
9. **Copy this token and save it securely** — this token **NEVER expires**

---

## Step 5: Update appsettings.json

Open `LeatherShopAPI/appsettings.json` and replace the WhatsApp section:

```json
"WhatsApp": {
    "PhoneNumberId": "HIS_NEW_PHONE_NUMBER_ID",
    "BusinessAccountId": "HIS_BUSINESS_ACCOUNT_ID",
    "AccessToken": "THE_PERMANENT_TOKEN_FROM_STEP_4",
    "VerifyToken": "REDACTED_VERIFY_TOKEN",
    "ApiVersion": "v22.0"
}
```

| Field | What to Put | Where to Get |
|-------|-------------|--------------|
| `PhoneNumberId` | Business owner's phone number ID | Step 3 — API Setup page |
| `BusinessAccountId` | Business owner's WhatsApp Business Account ID | Step 3 — API Setup page |
| `AccessToken` | Permanent System User token | Step 4 — the generated token |
| `VerifyToken` | Keep the same custom string | No change needed |
| `ApiVersion` | Keep `v22.0` | No change needed |

---

## Step 6: Update the Webhook

1. Go to [developers.facebook.com](https://developers.facebook.com/) → your app → **WhatsApp** → **Configuration**
2. Under **Webhook** → click **Edit**
3. Set **Callback URL** to your API webhook endpoint:
   - **Development:** `https://YOUR-NGROK-URL/api/whatsapp/webhook`
   - **Production:** `https://YOUR-PRODUCTION-API/api/whatsapp/webhook`
4. Set **Verify token** to: `REDACTED_VERIFY_TOKEN` (must match `appsettings.json`)
5. Click **Verify and Save**
6. Under **Webhook fields** → make sure **messages** is subscribed (toggle ON)

---

## Step 7: Test

1. Make sure your API is running (ngrok + `dotnet run`)
2. Send **"Hi"** from **any** WhatsApp number to the business owner's number
3. The chatbot should reply with the main menu
4. Verify:
   - ✅ No 5-user limit — anyone can message
   - ✅ No token expiry — permanent token works indefinitely
   - ✅ Blue tick shows next to business name
   - ✅ Business name (e.g., "LeatherShop") displayed instead of phone number

---

## Quick Checklist

- [ ] Business owner invites you as Admin to his Meta Business
- [ ] You accept the invitation via email
- [ ] You add his phone number to your Meta Developer App
- [ ] He gives you the OTP for phone verification
- [ ] You note the new Phone Number ID and Business Account ID
- [ ] You create a System User and generate a permanent token
- [ ] You update `appsettings.json` with the 3 new values
- [ ] You update the webhook URL in the app's Configuration page
- [ ] You test by messaging his business number from any WhatsApp

---

## What Customers See After Setup

```
┌─────────────────────────────┐
│ 🔵 LeatherShop  ✓           │  ← Blue tick + business name
│ Business Account             │
├─────────────────────────────┤
│ Welcome to Leather Shop!     │
│ How can we help you today?   │
│                              │
│ ┌─────────────────────────┐  │
│ │ 📋 View Menu            │  │  ← Chatbot works exactly the same
│ └─────────────────────────┘  │
└─────────────────────────────┘
```

---

## Troubleshooting

| Problem | Cause | Fix |
|---------|-------|-----|
| Webhook verification fails | Verify token mismatch | Ensure `VerifyToken` in `appsettings.json` matches the token entered in Meta Configuration |
| Messages not received | Webhook field not subscribed | Go to Configuration → Webhook fields → enable **messages** |
| 401 Unauthorized from Meta API | Token expired or wrong | Use the permanent System User token from Step 4 |
| Can't add phone number | Not admin of the business | Business owner must complete Step 1 first |
| Old test number still sending | "From" dropdown wrong | Change the "From" dropdown in API Setup to the new number |
| Blue tick not showing | Business not verified by Meta | Business owner needs to complete Meta Business Verification |
