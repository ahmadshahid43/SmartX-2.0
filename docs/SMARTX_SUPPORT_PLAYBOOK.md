# SmartX Technical and Functional Support Playbook

## Support scope

This playbook is for implementation teams, support staff, and client success users. It covers functional triage, local runtime checks, data safety, and escalation information. Do not ask clients to edit JSON files, change passwords, or modify permissions without an authorized owner.

## System map

| Layer | Local address / responsibility |
| --- | --- |
| SmartX web | `http://localhost:4200` user interface |
| SmartX API | `http://localhost:5163` business logic, authorization, local data APIs |
| Health check | `http://localhost:5163/health` service availability |
| Runtime storage | `.artifacts/runtime/foundation.local.json` in local mode |
| Web build | `.artifacts/web-dist/omnibusiness-web/browser` |

## First-response procedure

1. Capture the screen, exact page URL, time, affected user email, and the action attempted.
2. Check `http://localhost:5163/health`. If unavailable, start or restart the API before investigating the web page.
3. Confirm `http://localhost:4200/login` loads from the correct local server.
4. Verify role, branch, and enabled modules. Do not assume a client account has Owner access.
5. Identify whether the problem affects one user, one browser, one branch, or every user.
6. Preserve runtime data before any repair that might rewrite the local snapshot.

## Functional support matrix

| Area | Ask the user | Validate | Normal resolution |
| --- | --- | --- | --- |
| Login | Email, role, exact error | API health, correct localhost web proxy, account active | Hard reload page, verify credentials, clear stale session only with approval |
| POS | Product/customer/payment step | POS module access, active terminal, product stock | Refresh terminal, choose customer, add stock/product as needed |
| Customer | Existing or new customer? | Customer Hub access, saved customer list | Select from Active Bill picker or use Quick Add |
| Cart | SKU/product and requested quantity | In-hand minus reserved quantity | Adjust quantity or replenish stock |
| Hold | Ticket number | Cart has items, user role | Resume matching ticket or create a new hold |
| Booking | Customer, due date, advance | Booking status and balance | Collect installment or complete only when balance is settled |
| Refund | Sale reference and reason | User role, sale status, FBR state | Owner/Manager/Back Office performs controlled refund |
| Tax | Tender type, rate, exemption reason | Standard/Card tax inputs and final invoice tax | Correct rate before payment; use zero-tax only for approved exempt sale |
| Print | Invoice/slip, browser | Popup permission and printer availability | Allow popups, test browser print, then printer driver |
| Excel import | File and row number | Required headers and SKU uniqueness | Correct headers/numbers, re-import a small sample first |
| FBR | Sale reference and queue status | FBR config, connection, invoice state | Keep queued records, retry after service/config confirmation |

## POS customer support procedure

If Active Bill shows only Walk-in Customer:

1. Select the customer card in Active Bill.
2. Search name, phone, or email.
3. Select a result to attach it to the current bill.
4. If no customer exists, use Quick Add with at least a name. Phone/email are optional but recommended.
5. Confirm the selected name appears in Active Bill before payment.
6. If the selector is empty after adding customers, verify the API is updated and the web page was hard refreshed.

The selected customer is stored as the active customer for the transaction. The invoice, sale history, held sale, and normal checkout use that selected customer identity.

## Local runtime incident guide

| Symptom | Likely cause | Safe action |
| --- | --- | --- |
| `localhost refused to connect` | API or web server not running | Start API first, then web server; check health endpoint |
| API healthy, login fails | Web server not proxying `/api` or stale browser state | Ensure only one 4200 server is running; hard refresh browser |
| `npm.ps1` scripts disabled | Corporate PowerShell execution policy | Use `npm.cmd` or provided `.cmd` runner; do not weaken company policy |
| SQL Server unavailable | Domain laptop restriction | Keep LocalJson provider; do not install SQL Server as a workaround |
| API build files locked | API process running from build output | Use isolated verification output or stop API during deployment update |
| Angular native build crash | Corporate machine/Node native bundler issue | Use a compatible Node LTS runtime or compiled static build with local API proxy |

## Data safety and escalation

- Take a dated backup of local runtime data before migrations, bulk imports, version upgrades, or data repair.
- Never delete a runtime snapshot to solve a UI issue.
- Never manually modify password hashes or production access tokens.
- Refund, stock reversal, plan/module changes, and role changes should include an audit note and authorized approver.
- For potential FBR compliance issues, record sale reference, timestamp, FBR queue/result, branch, tax amount, and operator. Escalate to the business compliance owner.

## Information to collect for engineering

Provide this set in one support ticket:

- user email and role;
- business/tenant and branch;
- browser and Windows version;
- page URL and local time of issue;
- exact action sequence;
- screenshot or screen recording;
- sale/booking/hold/transfer/GRN reference where relevant;
- API health result;
- whether other users can reproduce it;
- whether the issue started after import, upgrade, role change, or restart.

Do not include passwords, access tokens, full payment card data, or customer medical/personal data in a support ticket.

## Release verification checklist

- API health returns `200`.
- Owner, Manager, Cashier, and client role landing pages behave correctly.
- Login, logout, and session restore work.
- POS can select Walk-in, saved customer, and Quick Add customer.
- Product add, quantity update, hold/resume, booking, split payment, checkout, and invoice/slip print work.
- A refund follows role restrictions and optionally returns inventory.
- Inventory import uses a small sample before full import.
- Plans/modules block disabled screens and APIs.
- Desktop/local mode does not require SQL Server or internet for standard counter sale operation.
