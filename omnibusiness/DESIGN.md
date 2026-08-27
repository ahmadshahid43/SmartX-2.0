---
name: OmniBusiness
colors:
  surface: '#fcf8fa'
  surface-dim: '#dcd9db'
  surface-bright: '#fcf8fa'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f6f3f5'
  surface-container: '#f0edef'
  surface-container-high: '#eae7e9'
  surface-container-highest: '#e4e2e4'
  on-surface: '#1b1b1d'
  on-surface-variant: '#45464d'
  inverse-surface: '#303032'
  inverse-on-surface: '#f3f0f2'
  outline: '#76777d'
  outline-variant: '#c6c6cd'
  surface-tint: '#565e74'
  primary: '#000000'
  on-primary: '#ffffff'
  primary-container: '#131b2e'
  on-primary-container: '#7c839b'
  inverse-primary: '#bec6e0'
  secondary: '#505f76'
  on-secondary: '#ffffff'
  secondary-container: '#d0e1fb'
  on-secondary-container: '#54647a'
  tertiary: '#000000'
  on-tertiary: '#ffffff'
  tertiary-container: '#271901'
  on-tertiary-container: '#98805d'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#dae2fd'
  primary-fixed-dim: '#bec6e0'
  on-primary-fixed: '#131b2e'
  on-primary-fixed-variant: '#3f465c'
  secondary-fixed: '#d3e4fe'
  secondary-fixed-dim: '#b7c8e1'
  on-secondary-fixed: '#0b1c30'
  on-secondary-fixed-variant: '#38485d'
  tertiary-fixed: '#fcdeb5'
  tertiary-fixed-dim: '#dec29a'
  on-tertiary-fixed: '#271901'
  on-tertiary-fixed-variant: '#574425'
  background: '#fcf8fa'
  on-background: '#1b1b1d'
  surface-variant: '#e4e2e4'
typography:
  display-lg:
    fontFamily: Inter
    fontSize: 30px
    fontWeight: '700'
    lineHeight: 38px
    letterSpacing: -0.02em
  headline-md:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
    letterSpacing: -0.01em
  headline-sm:
    fontFamily: Inter
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  body-lg:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  body-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  body-sm:
    fontFamily: Inter
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 18px
  label-md:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.05em
  data-tabular:
    fontFamily: JetBrains Mono
    fontSize: 14px
    fontWeight: '500'
    lineHeight: 20px
  data-tabular-sm:
    fontFamily: JetBrains Mono
    fontSize: 12px
    fontWeight: '500'
    lineHeight: 16px
rounded:
  sm: 0.125rem
  DEFAULT: 0.25rem
  md: 0.375rem
  lg: 0.5rem
  xl: 0.75rem
  full: 9999px
spacing:
  base: 8px
  xs: 4px
  sm: 8px
  md: 16px
  lg: 24px
  xl: 32px
  gutter: 16px
  margin-mobile: 16px
  margin-desktop: 32px
---

## Brand & Style
The design system is engineered for high-performance enterprise environments where clarity, speed of data entry, and long-term visual comfort are paramount. It follows a **Minimalist Corporate** aesthetic, prioritizing functional density over decorative elements.

The target audience consists of operations managers, accountants, and retail staff who require a tool that feels reliable and invisible. The emotional response is one of "ordered efficiency"—the UI should feel like a precise instrument. To achieve this, the design system utilizes high whitespace efficiency, a restrained color palette, and a focus on systematic alignment.

## Colors
This design system employs a sophisticated, low-fatigue palette. 

- **Primary & Secondary:** Use the Deep Enterprise Blue (#0F172A) for text and primary actions to establish authority. Use Slate (#64748B) for secondary information and iconography.
- **Accents:** Indigo (#4F46E5) is reserved for interactive highlights, focus states, and primary call-to-actions to provide a "SaaS-native" feel.
- **Semantic Colors:** Success, Warning, Error, and Info colors are used sparingly for status indicators and validation.
- **Surfaces:** Use #F8FAFC for the main application background to reduce glare. Use pure #FFFFFF for cards, tables, and input areas to create clear containment.

## Typography
The typography system is built on **Inter** for its exceptional legibility in UI contexts. 

- **Scale:** A tight scale is used to accommodate high information density. 14px is the standard body size, while 13px is used for dense sidebars or secondary metadata.
- **Data Display:** For financial figures, inventory counts, and SKU numbers, use **JetBrains Mono**. The monospaced nature ensures that columns of numbers align perfectly in tables, facilitating quick visual scanning of balances and quantities.
- **Hierarchy:** Use font weight rather than size to establish hierarchy in dense forms. Bold (600/700) should be reserved for section headers and primary identifiers.

## Layout & Spacing
The layout follows a strict **8px grid system** to ensure mathematical harmony across all components.

- **Grid Model:** Use a 12-column fluid grid for main content areas. In ERP views, sidebars should be fixed (240px or 280px) while the main data area remains fluid.
- **Information Density:** For "Comfortable" views, use 16px (md) padding. For "Compact" data tables, reduce cell padding to 8px (sm) or 4px (xs) vertically.
- **Breakpoints:**
  - Mobile: < 640px (1 column, 16px margins)
  - Tablet: 640px - 1024px (Stacked dashboard widgets)
  - Desktop: > 1024px (Full 12-column availability)

## Elevation & Depth
This design system utilizes **Tonal Layers** and **Low-Contrast Outlines** rather than heavy shadows to maintain a clean, professional aesthetic.

- **Level 0 (Background):** #F8FAFC. Used for the application canvas.
- **Level 1 (Surface):** #FFFFFF with a 1px border of #E2E8F0. Used for cards, whiteboards, and table containers.
- **Level 2 (Overlay):** Used for modals and dropdowns. Use a subtle, highly diffused shadow: `0px 4px 12px rgba(15, 23, 42, 0.08)` and a #E2E8F0 border.
- **Interactive Depth:** When an item is hovered, change the border color to #CBD5E1 (Slate 300) rather than increasing shadow depth.

## Shapes
The shape language is **Soft (0.25rem)**. This provides a modern, approachable feel while maintaining the structural rigour required for enterprise software. 

- **Standard (4px):** Used for input fields, checkboxes, and buttons.
- **Large (8px):** Used for cards and main content containers.
- **Extra Large (12px):** Reserved for major modal containers.
- **Pill:** Strictly reserved for status "Chips" or "Badges" to distinguish them from interactive buttons.

## Components
- **Buttons:** Primary buttons use #0F172A with white text. Secondary buttons use a white fill with #E2E8F0 border and #0F172A text. Use a 2px Indigo focus ring for accessibility.
- **Input Fields:** 1px #E2E8F0 border, 8px vertical padding. Focus state switches border to #4F46E5 with a soft glow.
- **Data Tables:** Use #FFFFFF background. Header row should have a subtle #F1F5F9 background with `label-md` typography. Rows should have a 1px bottom border of #F1F5F9.
- **Chips/Status:** Use low-saturation background tints of the semantic colors (e.g., Success background: #DCFCE7, Text: #166534).
- **Navigation:** Vertical left-hand navigation is preferred for ERP scale. Active states should be marked by a 3px Indigo vertical bar on the left edge of the menu item.
- **Inventory/POS Cards:** Use a flat design with a 1px border. Product images should have a 4px corner radius and a light gray neutral background to normalize varying asset quality.