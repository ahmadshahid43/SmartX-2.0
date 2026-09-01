# SmartX ERP + POS User Guide

## 1. Product purpose

SmartX is a local-first ERP and POS workspace for retail, pharmacy, distribution, and service businesses. It supports day-to-day counter sales while also covering inventory, customer records, purchase activity, warehouse workflows, users, reports, plans, and permissions.

The default office-laptop mode stores data locally through the LocalJson provider. It does not require SQL Server or an internet connection for daily operations. Supabase/Postgres is an optional future deployment mode.

## 2. Starting SmartX

1. Open the project folder.
2. Start `run-api.cmd` and wait until the API health endpoint responds at `http://localhost:5163/health`.
3. Start the web application using `run-web.cmd`. If the corporate laptop blocks the Angular dev server, use the local proxy runner prepared for the compiled web build.
4. Open `http://localhost:4200/login`.
5. Sign in using an account created in Users & Access.

Default local administrator for first setup:

| Field | Value |
| --- | --- |
| Email | `admin@omnibusiness.local` |
| Password | `Admin@123` |
| Role | Owner |

Change the owner password before any public deployment.

## 3. Roles and access

| Role | Typical responsibility |
| --- | --- |
| Owner | Full workspace control, plans, modules, users, configuration, refunds |
| Manager | Store operations, inventory, reports, customer and sales supervision |
| Cashier | POS, customer selection, bookings, payments, invoice and slip printing |
| Back Office | Finance, reports, inventory review, approved refund/FBR work |

Use **Users & Access** to create staff accounts, assign the role, branch, status, and module permissions. A client account should normally be Cashier or Manager, not Owner.

## 4. Daily POS workflow

1. Open **POS**.
2. Select the bill customer from the **Active Bill** customer card.
3. Search a saved customer by name, phone, or email. Select **Walk-in** for anonymous retail sales.
4. For a new customer, enter name and optional phone/email, then choose **Add & Select**. The record becomes available in the customer list for later bills.
5. Add products by card, SKU search, barcode/manual entry, or Quick Picks.
6. Change quantity with the `+` and `-` controls. Remove an item with `x`.
7. Use **Hold Current Sale** to park a bill. Use the held-ticket list to resume it later.
8. Use **Book Order** for advance, later delivery, or installment orders. Add customer details and an optional deposit.
9. Select **Take Payment**. Choose Cash, Card, Bank Transfer, Digital Wallet, or split payment.
10. Set **Standard tax %** and **Card tax %** according to the business's approved tax policy. Card tender automatically uses the Card tax rate.
11. Enable **Tax exempt / zero tax for this bill** only for a valid exempt/zero-tax transaction approved by the business. This keeps the discount line separate from tax rather than hiding tax inside a discount.
12. Enter collection amount and reference numbers where relevant. SmartX calculates change and balance.
13. Enable FBR queue only when the business has configured and approved its FBR process. Complete the payment.
14. Print the full invoice or thermal slip after payment. Browser popup permission must be allowed for printing.

## 5. Customer handling

- **Walk-in customer**: use for counter customers when contact history is not required.
- **Saved customer**: preserves pricing tier, contact information, purchase history, loyalty-related values, and billing identity.
- **Quick add at POS**: creates a reusable customer while making the current bill belong to that customer.
- **Booked order**: always capture name, and preferably phone/email, so installment collection and invoice sharing can be traced.

## 6. Inventory and catalog

Use **Inventory** to manage product master data and stock.

- Add or update SKU, product name, category, warehouse, price, opening stock, reorder level, and quick-sale/favourite status.
- Use **Import Excel / CSV** for bulk onboarding. Required columns are `SKU` and `Name`; common optional columns include `Category`, `Unit Price`, `Warehouse`, `In Hand`, `Reserved`, `Reorder Level`, `Is Favorite`, `Is Quick Sale`, and `Visual Code`.
- Existing SKU values are updated during import; new SKU values become new products.
- Use stock adjustment for receiving corrections, damages, shrinkage, or count differences. Always enter a clear reason.
- Review low-stock, inventory value, warehouse, and category summaries before reordering.

## 7. Warehouse and procurement

- **Procurement**: manage suppliers/vendors, purchase orders, order status, and expected replenishment.
- **Warehouse**: record stock transfers, goods receipts (GRN/inward), and gate passes for controlled stock movement.
- **Stock transfer**: select source/destination, products, quantity, and reference. Confirm both sides before marking complete.
- **GRN / goods receipt**: record vendor reference, received date, accepted quantity, and any discrepancy.
- **Gate pass**: use for outward movement, service/repair dispatch, branch transfer, or customer return movement.

## 8. Sales, returns, and FBR queue

- **Sales Command Center** shows net sales, transaction count, payment mix, bookings due, and receipt history.
- Open a sale to review item lines, payment allocation, customer, FBR status, and receipt details.
- Refunds are restricted to Owner, Manager, and Back Office roles. Enter the reason and choose whether inventory should be returned to stock.
- FBR queue is offline-capable: queued status means the transaction needs later submission/confirmation according to the configured integration process. It is not proof of an accepted government invoice until a confirmed FBR reference is available.

## 9. Plans and module control

Use **Plans & Modules** to create commercial plans and decide which modules are available to a tenant/client.

1. Create plan name, price, included users, and included modules.
2. Assign the plan to a client workspace.
3. Turn each module on or off per client where commercial terms require it.
4. Test the client role after changes. Disabled modules should not be reachable through navigation or direct links.

## 10. Reports and operations

Use Dashboard, Sales, Inventory, Operations Hub, and Procurement data to review:

- daily, weekly, monthly sales and gross profit;
- payment-type trends and sale count;
- best/worst selling products and category movement;
- low stock, stock valuation, stock usage, and turnover indicators;
- booking balances and pending installment collections;
- purchase, expense, supplier, and warehouse movement information.

Before sharing reports, confirm date range, branch, refunded sales treatment, and whether values are gross or net of discount/tax.

## 11. Safe operating checklist

- Start API before web application.
- Confirm the correct branch and customer before payment.
- Do not use refund to correct a simple quantity error before checking whether the sale is already completed.
- Never give Owner credentials to a client cashier.
- Back up local runtime data before a major update or migration.
- Test FBR, printer, email, WhatsApp, payment gateway, and social integrations in a non-production workspace before rollout.

## 12. Quick troubleshooting

| Problem | First check |
| --- | --- |
| Login fails | API health, correct server on port 4200, email/password, old browser cache |
| POS does not load | API is running, authenticated role has POS access, browser can reach `/api` |
| Only walk-in appears | Open Active Bill customer selector, load saved customers, use Quick Add when customer list is empty |
| Print window does not open | Allow popups for localhost, then retry invoice/slip print |
| Excel import fails | Check SKU/Name headers, file type, duplicate rows, and number formatting |
| FBR stays queued | Confirm integration configuration and internet/FBR availability, then submit from sales workflow |

For detailed technical diagnosis, use [SMARTX_SUPPORT_PLAYBOOK.md](SMARTX_SUPPORT_PLAYBOOK.md).
