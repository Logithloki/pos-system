# 🧾 Retail POS System – Full AI Build Specification

## 🎯 Objective
Build a fully functional, production-ready **Retail Point of Sale (POS) System** with inventory management, CRM, and reporting.

The system MUST be:
- Fully working end-to-end
- Secure by design
- Bug-free (AI must validate and test logic)
- Clean, scalable, and maintainable

---

# 🏗️ SYSTEM TYPE

Build as a **Desktop Application (EXE)**

Recommended Stack:
- Frontend: Electron.js OR .NET (WPF / WinForms)
- Backend: Node.js (Express) OR .NET Core
- Database: SQLite (local) or MySQL
- ORM: Prisma / Entity Framework
- Architecture: Clean Architecture (MVC or layered)

---

# 🔐 SECURITY REQUIREMENTS (MANDATORY)

The system MUST include:

### Authentication & Authorization
- Secure login system
- Password hashing (bcrypt or equivalent)
- Role-based access control:
  - Admin
  - Cashier

### Data Protection
- Input validation (prevent SQL injection, XSS)
- Parameterized queries ONLY
- Sanitization of all user inputs

### System Security
- No hardcoded credentials
- Environment-based configuration
- Secure session handling
- Logging for all critical actions

### Additional
- Prevent negative stock
- Prevent duplicate transactions
- Ensure transactional integrity (ACID compliance)

---

# 🧾 CORE FEATURES

## 1. Checkout Processing
- Add products to cart (manual + barcode)
- Quantity adjustment
- Auto total calculation
- Apply discounts
- Select payment method:
  - Cash
  - Credit (manual entry)
- Generate receipt

## 2. Receipt System
- Generate printable receipt
- Include:
  - Store name
  - Items list
  - Total
  - Payment method
  - Date/time
- Support thermal printer format

---

# 📦 INVENTORY MANAGEMENT

## Features:
- Add / Edit / Delete products
- Fields:
  - Name
  - Barcode
  - Price
  - Cost
  - Stock quantity
  - Supplier

## Functionalities:
- Real-time stock updates after sale
- Barcode scanning support
- Low stock alert system
- Prevent selling out-of-stock items

---

# 🚚 SUPPLIER MANAGEMENT

- Add supplier details:
  - Name
  - Contact
  - Address
- Link products to suppliers
- Track restocking

---

# 👥 CRM SYSTEM

## Customer Management:
- Add customer profile:
  - Name
  - Phone
  - Email (optional)

## Features:
- Track purchase history
- Loyalty system:
  - Points per purchase
  - Redeem points
- View customer transactions

---

# 📊 REPORTING & ANALYTICS

## Required Reports:
- Daily / Weekly / Monthly sales
- Profit margin calculation:
  Profit = Selling Price - Cost
- Inventory turnover
- Low stock report

## Dashboard:
- Total sales
- Top-selling products
- Revenue summary

---

# 🧠 BUSINESS RULES

- Stock must decrease after every sale
- Cannot sell if stock = 0
- Each transaction must be saved permanently
- All monetary calculations must be accurate (no floating errors)

---

# 🖥️ UI/UX REQUIREMENTS

- Clean and modern UI
- Fast cashier interface
- Keyboard shortcuts support
- Responsive layout
- Minimal clicks for checkout

---

# 🧪 TESTING REQUIREMENTS

The AI MUST:
- Test all modules
- Validate:
  - Checkout flow
  - Inventory updates
  - Report accuracy
- Handle edge cases:
  - Zero stock
  - Invalid inputs
  - Large transactions

---

# ⚙️ PERFORMANCE REQUIREMENTS

- Fast response time (< 200ms operations)
- Efficient database queries
- No memory leaks

---

# 📁 PROJECT STRUCTURE

Must include:
- Modular folder structure
- Separation of concerns
- Reusable components
- Config files (.env)

---

# 🚫 STRICT RULES

- NO placeholder logic
- NO incomplete features
- NO TODO comments
- NO hardcoded values
- NO security vulnerabilities

---

# ✅ FINAL OUTPUT REQUIREMENTS

The AI MUST:
1. Generate full working source code
2. Provide database schema
3. Provide setup instructions
4. Ensure the system runs without errors
5. Ensure all features are implemented completely

---

# 🧠 AI EXECUTION INSTRUCTION (IMPORTANT)

You are a senior software engineer.

You must:
- Think step-by-step before coding
- Design the system before implementation
- Validate every module
- Ensure production-level quality
- Avoid shortcuts

If any ambiguity exists, make the best engineering decision.

---

# 🚀 END GOAL

Deliver a **fully working, installable POS system (EXE)** with:
- Checkout
- Inventory
- CRM
- Reporting
- Security

System must be stable, secure, and ready for real business use.