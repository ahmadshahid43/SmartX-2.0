# SmartX QA, Optimization, and UI Prompt Pack

## Purpose

Use this document when you want another developer, QA engineer, AI coding agent, or UI designer to review, improve, optimize, and polish the SmartX ERP + POS system end to end.

This pack is tailored to the current project constraints:

- brand name is `SmartX`
- system must run on a domain-joined office laptop
- SQL Server is not available right now
- app must stay runnable with local/offline-friendly storage
- future migration back to SQL must remain easy
- POS must support Pakistan retail/pharmacy style workflows
- web and desktop experiences both matter

## Current project context

- Backend: ASP.NET Core API
- Frontend: Angular web app
- Desktop: WPF shell
- Current storage strategy: local JSON persistence
- Runtime storage must remain safe for office-laptop restrictions
- Login must support both username-style input and full email
- Role-based access is required
- Client users must not see owner-only/admin-only modules

## What QA must cover

### Functional QA

- login with admin, cashier, and client-style user accounts
- login by full email and by username where supported
- logout and session restore behavior
- role-based menu visibility and route protection
- dashboard load behavior
- POS product search, manual add to bill, quantity change, discount, tax, subtotal, total
- hold sale, resume sale, void sale
- split payment and multiple payment methods
- customer details during sale: name, phone, email
- invoice generation, thermal slip print, and popup-print behavior
- refund and return flows
- order listing and sales history
- inventory add/update
- category and barcode flow
- stock take, GRN, low stock, valuation, and usage screens
- Excel inventory import for new users
- user creation, role assignment, and permission control
- plans and module restriction behavior
- dark mode and light mode consistency
- responsive behavior on laptop, tablet, and mobile widths

### Technical QA

- API health endpoints
- auth token handling
- route guards
- persistence behavior when runtime file does not exist
- local JSON runtime file creation and update
- offline-safe behavior on managed office laptop
- no hidden SQL dependency anywhere in startup flow
- run scripts: `run-api.cmd`, `run-web.cmd`, `run-desktop.cmd`, `run-demo.cmd`
- build flow for API and Angular app
- permission issues around build output, temp files, and local runtime paths

### UX and UI QA

- spacing consistency
- broken layouts on small screens
- sidebar behavior
- card density and visual hierarchy
- readability in dark mode
- form clarity and empty states
- consistency of SmartX branding
- polish of POS interaction flow
- support widget usefulness
- visual consistency between login, dashboard, POS, inventory, plans, and users screens

### Performance and code quality QA

- duplicated logic in services and guards
- over-fetching or repeated API calls
- missing loading states and error states
- unnecessary bundle size growth
- weak typing or fragile data mapping
- missing tests around auth, role access, import, and POS workflows
- poor separation between UI state and business logic
- brittle local-storage or token handling

## Enhancement priorities

### High priority

- make every critical business flow actually usable, not only visually present
- complete POS cashier flow
- improve receipt and invoice workflow
- ensure proper user roles and client restrictions
- ensure Excel import works for real onboarding
- stabilize offline/local-first storage
- finish responsive layouts for every main screen

### Medium priority

- better empty states and onboarding helpers
- stronger search, filter, and keyboard shortcuts
- clearer plan/module management UI
- support center with guided help
- smarter validation and better user feedback

### Nice to have

- advanced analytics cards
- contextual coaching inside screens
- printable audit reports
- richer animations where they improve clarity
- desktop shell parity with web flows

## Master QA + Optimization Prompt

Copy and use this prompt as-is with an AI coding agent, QA team, or senior engineer:

```text
You are a senior full-stack engineer, QA lead, product designer, and software architect reviewing and improving a production-style ERP + POS application called SmartX.

Your job is to perform a full product QA audit, identify all broken or incomplete functionality, optimize the codebase, improve UX where needed, and leave the project in a stable, production-ready state.

Project context:
- Brand name: SmartX
- Domain: configurable ERP + POS for Pakistan retail/pharmacy-style businesses
- Backend: ASP.NET Core API
- Frontend: Angular web application
- Desktop: WPF shell
- SQL Server is NOT available right now because the app must run on a managed office laptop
- The system must run without SQL using local/offline-friendly persistence
- The persistence architecture must stay easy to migrate back to SQL later
- The system must support both web and desktop-friendly workflows
- Role-based access is mandatory
- Client users must not see owner-only/admin-only controls
- The UX must feel premium, modern, intentional, and operationally useful, not like a static demo

Primary goals:
1. Perform deep functional QA across auth, dashboard, POS, inventory, users, plans, printing, support, and reporting areas.
2. Identify incomplete, fake, placeholder, broken, or purely visual-only flows.
3. Fix bugs and close functional gaps where feasible.
4. Optimize code structure, performance, state handling, and maintainability.
5. Improve responsive behavior for laptop, tablet, and mobile widths.
6. Review dark mode and light mode for consistency and readability.
7. Preserve local/offline operation without introducing any SQL dependency.
8. Keep the architecture ready for a future SQL repository implementation behind existing abstractions.

Critical business flows to validate and improve:
- Login with admin and non-admin users
- Username or email sign-in support
- Session restore and logout
- Role-based navigation and route protection
- POS cashier workflow
- Product search and manual add-to-bill when scanner is unavailable
- Discount, tax, subtotal, total, split payment, and late payment flows
- Hold order, resume order, and void sale
- Customer details during billing
- Invoice generation and thermal slip printing
- Popup blocker handling for printing
- Refund/return behavior
- Inventory CRUD and stock visibility
- Excel-based inventory import for onboarding new clients
- Categories, barcode, stock take, GRN, warehouse reports, and valuation
- Users, employees, permissions, and module restriction by plan
- Plans and per-module entitlements
- Support/help experience
- FBR/offline queue readiness
- Desktop launch readiness

Technical review requirements:
- Verify run scripts and developer startup flow
- Verify API health and readiness
- Verify local JSON persistence and writable paths
- Eliminate permission-sensitive output/runtime locations where possible
- Review build output configuration and startup reliability on managed Windows machines
- Reduce duplication and improve naming consistency
- Tighten validation and error handling
- Improve test coverage for auth, roles, inventory import, and POS workflows
- Note architectural risks and future migration concerns

UI/UX review requirements:
- Do not keep bland or generic dashboard styling
- Make the product look like a premium operational platform
- Improve spacing, consistency, density, hierarchy, and screen balance
- Remove awkward padding, layout breaks, and dead visual areas
- Keep the login experience polished and aligned with the full product theme
- Make POS flow faster and more cashier-friendly
- Ensure all primary screens feel related as one design system

Expected output:
- A prioritized bug/findings list with severity
- Implemented fixes where possible
- A list of remaining gaps
- Suggested next milestones
- Clear notes about what was changed in code
- Verification steps for each major fix

Do not give shallow feedback. Treat this as a real product audit and improvement pass.
```

## Super UI Redesign Prompt

Use this when you want a strong visual and UX upgrade without losing business usability:

```text
Redesign the SmartX ERP + POS interface as a premium, modern, enterprise-grade operational product for Pakistan retail and pharmacy businesses.

Important constraints:
- Keep the product name SmartX
- Support both light mode and dark mode
- Maintain role-based workflows
- Do not break existing business flows
- Design for cashier speed, back-office clarity, and owner-level oversight
- The UI must be fully responsive across large desktop, small laptop, tablet, and mobile widths
- Avoid generic AI-looking layouts and avoid bland admin dashboard aesthetics

Design direction:
- Create a unified design language across login, dashboard, POS, inventory, users, plans, support, and reporting
- Use intentional spacing, sharper hierarchy, stronger typography, better card balance, cleaner tables, and clearer forms
- Make the POS feel fast, focused, and keyboard-friendly
- Make inventory and users screens feel operational rather than decorative
- Improve empty states, loading states, error states, and confirmation feedback
- Ensure plan/module controls feel premium and understandable
- Keep SmartX branding consistent in invoices, receipts, slips, support areas, and account surfaces

Business UX expectations:
- If scanner hardware is unavailable, manual product add must feel first-class
- Invoice and slip print actions must be obvious and reliable
- Customer details, payment method, discounts, taxes, and balance flows must be intuitive
- Client users should only see what their role and plan allow
- Admin/owner screens should feel more powerful without becoming cluttered

What to improve screen by screen:
- Login: large, polished, memorable, animated, but still professional and readable
- Dashboard: real operational overview, not empty marketing cards
- POS: fast cashier layout, better product search, clearer cart, stronger payment area
- Inventory: stronger tables, filters, import actions, stock visibility, warehouse context
- Users & Access: clearer roles, permissions, plan restrictions, and client-safe presentation
- Plans & Modules: visually premium pricing and entitlement management
- Support: better help panel, chatbot framing, and guided quick actions

Implementation quality requirements:
- Use reusable tokens, variables, and component patterns
- Improve responsiveness instead of only desktop styling
- Reduce visual inconsistency and arbitrary padding
- Keep accessibility, contrast, and keyboard usability in mind
- Prefer purposeful motion over excessive animation

Deliverable:
- A fully updated UI pass with consistent SmartX theming
- Notes on what changed and why
- Screens that feel production-ready, not like placeholder demos
```

## Quick handoff message for a QA person

If you want a short Urdu/Roman Urdu instruction for a QA person, use this:

```text
Is SmartX ERP + POS project ki complete QA karo. Sirf surface UI mat dekho, balkeh end-to-end flows test karo:

1. Login admin aur Ahmad jese non-admin user se test karo
2. Username aur email dono se login verify karo
3. Dashboard, POS, inventory, users, plans, support, printing, import aur role-based access check karo
4. Jo cheezen sirf screen par hain lekin actually kaam nahi kar rahi un sab ki list banao
5. Har bug ko severity ke sath report karo
6. Responsive, dark mode, light mode aur popup-print flows bhi test karo
7. Ye bhi confirm karo ke system SQL ke baghair office laptop par run kar raha ho
8. Agar code ya UX optimization ki zarurat ho to clear recommendations do
```

## Recommended next execution order

1. Functional QA pass
2. Bug fixing pass
3. UX/UI polish pass
4. Performance and code optimization pass
5. Regression QA pass
6. Desktop parity pass

## Internal note

If login or persistence behavior is rechecked in future, validate against the current SmartX local-first storage strategy and current role restrictions, not older OmniBusiness defaults.
