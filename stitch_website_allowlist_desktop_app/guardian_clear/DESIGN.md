---
name: Guardian Clear
colors:
  surface: '#f8f9ff'
  surface-dim: '#ccdbf4'
  surface-bright: '#f8f9ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#eff4ff'
  surface-container: '#e6eeff'
  surface-container-high: '#dde9ff'
  surface-container-highest: '#d5e3fd'
  on-surface: '#0d1c2f'
  on-surface-variant: '#3e4947'
  inverse-surface: '#233144'
  inverse-on-surface: '#ebf1ff'
  outline: '#6e7977'
  outline-variant: '#bdc9c6'
  surface-tint: '#006a63'
  primary: '#005c55'
  on-primary: '#ffffff'
  primary-container: '#0f766e'
  on-primary-container: '#a3faef'
  inverse-primary: '#80d5cb'
  secondary: '#006c49'
  on-secondary: '#ffffff'
  secondary-container: '#6cf8bb'
  on-secondary-container: '#00714d'
  tertiary: '#004eaa'
  on-tertiary: '#ffffff'
  tertiary-container: '#0165d8'
  on-tertiary-container: '#e4eaff'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#9cf2e8'
  primary-fixed-dim: '#80d5cb'
  on-primary-fixed: '#00201d'
  on-primary-fixed-variant: '#00504a'
  secondary-fixed: '#6ffbbe'
  secondary-fixed-dim: '#4edea3'
  on-secondary-fixed: '#002113'
  on-secondary-fixed-variant: '#005236'
  tertiary-fixed: '#d8e2ff'
  tertiary-fixed-dim: '#adc6ff'
  on-tertiary-fixed: '#001a42'
  on-tertiary-fixed-variant: '#004395'
  background: '#f8f9ff'
  on-background: '#0d1c2f'
  surface-variant: '#d5e3fd'
typography:
  title-window:
    fontFamily: Inter, "Noto Sans TC", "Microsoft JhengHei", sans-serif
    fontSize: 13px
    fontWeight: '500'
    lineHeight: 20px
  headline-lg:
    fontFamily: Inter, "Noto Sans TC", "Microsoft JhengHei", sans-serif
    fontSize: 26px
    fontWeight: '700'
    lineHeight: 34px
    letterSpacing: -0.02em
  headline-md:
    fontFamily: Inter, "Noto Sans TC", "Microsoft JhengHei", sans-serif
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
    letterSpacing: -0.01em
  headline-sm:
    fontFamily: Inter, "Noto Sans TC", "Microsoft JhengHei", sans-serif
    fontSize: 16px
    fontWeight: '600'
    lineHeight: 24px
  body-lg:
    fontFamily: Inter, "Noto Sans TC", "Microsoft JhengHei", sans-serif
    fontSize: 15px
    fontWeight: '400'
    lineHeight: 24px
  body-md:
    fontFamily: Inter, "Noto Sans TC", "Microsoft JhengHei", sans-serif
    fontSize: 13.5px
    fontWeight: '400'
    lineHeight: 20px
  body-sm:
    fontFamily: Inter, "Noto Sans TC", "Microsoft JhengHei", sans-serif
    fontSize: 12px
    fontWeight: '400'
    lineHeight: 18px
  label-md:
    fontFamily: Inter, "Noto Sans TC", "Microsoft JhengHei", sans-serif
    fontSize: 13.5px
    fontWeight: '500'
    lineHeight: 18px
  label-sm:
    fontFamily: Inter, "Noto Sans TC", "Microsoft JhengHei", sans-serif
    fontSize: 11.5px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.02em
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  gutter: 1rem
  margin: 1.5rem
  space-xs: 0.25rem
  space-sm: 0.5rem
  space-md: 0.875rem
  space-lg: 1.25rem
  space-xl: 1.75rem
---

## Brand & Style

This design system is tailored for home protection, parental oversight, and family digital safety. Instead of looking like a cold, enterprise-grade firewall configuration console or intimidating IT security software, the visual language radiates warmth, patience, and absolute reliability. The interface acts as a gentle, reassuring digital guardian for parents, demystifying technical controls into clear, approachable decisions.

The design movement blends **Modern Corporate** with **Windows 11 Fluent Softness**:
- **Tactile Softness & Mica Warmth:** Subdued warm surfaces replace clinical cold grays. Layered window backdrops, soft strokes, and breathable containers establish an intuitive home-appliance simplicity.
- **Friendly Authority:** Visual metaphors favor home shields, warm badges, clear checkmarks, and plain-language notices rather than harsh stop signs or terminal-like outputs.
- **Non-Technical Clarity:** Interactive components boast generous hit areas, readable labeling, obvious toggle states, and visual progress breadcrumbs tailored for non-tech-savvy users navigating desktop setup flows.

## Colors

The color palette centers on trusted security and calm reassurance:
- **Primary (`#0F766E`)**: Deep balanced teal. Anchors the primary action buttons, key toggle active states, window branding, and primary progression controls.
- **Secondary (`#10B981`)**: Emerald green. Signals safety, active allowlists, healthy protection status, and verified URLs. Paired with soft mint tint `#ECFDF5` for status containers.
- **Tertiary (`#3B82F6`)**: Gentle slate blue. Dedicated to informational badges, active shield banners, help callouts, and educational tooltips.
- **Warm Accent / Alert (`#D97706` / `#C2410C`)**: Terracotta amber. Communicates blocked records, warnings, strict filtering advisories, and destructive confirmations. Supported by `#FFFBEB` background fills.
- **Neutrals**: Warm slate tones (`#1E293B` for high-emphasis headlines, `#334155` for body text, `#64748B` for helper notes, `#E2E8F0` for structural borders, and `#F8F9FA` to `#F1F4F5` for canvas layers).

## Typography

The type system prioritizes Traditional Chinese and Latin glyph balance using `Inter` alongside `Noto Sans TC` and `Microsoft JhengHei`. 

Key principles:
- **Optical Legibility:** Line heights are maintained at 1.45x–1.6x to give complex Traditional Chinese characters breathing room.
- **Hierarchical Calmness:** Headlines utilize structured weights (`600` and `700`) without excessive scale jumps to keep parents composed and focused.
- **Microcopy Guidance:** Labels and captions are set cleanly at `11.5px` to `13.5px` with distinct tonal contrasts, preventing cognitive overload during setup procedures.

## Layout & Spacing

The layout is built for a 1024x720 fixed-proportioned Windows desktop utility frame with responsive adaptability for window scaling.

Structure:
- **Window Chrome & Titlebar:** Top persistent bar at 40px height housing brand glyph, tool name (`title-window`), and standard Windows minimize, maximize, and close controls.
- **Two-Column Master Layout:**
  - Left Rail (240px fixed width): Houses workflow navigation, shield status summary, and parent profile lock indicators.
  - Primary Stage (remainder fluid width): Houses current step content, rule lists, allowlist inputs, and bulk import cards.
- **Content Grid & Padding:** Standard 16px (`gutter`) separation across internal card structures, enveloped by 24px (`margin`) outer padding. Vertical rhythm is governed strictly by the `0.25rem` to `1.75rem` spacing tokens to preserve predictable alignment.

## Elevation & Depth

Visual hierarchy uses Windows 11 Fluent layered depth without heavy drop shadows:
- **Canvas Base (`#F1F4F5`)**: The window frame and secondary surfaces.
- **Surface Elevation Level 1 (Card & Content Blocks)**: Pure `#FFFFFF` card layers encased with a crisp single-pixel boundary `border: 1px solid #E2E8F0` and subtle dual ambient diffusion: `box-shadow: 0 1px 3px rgba(0,0,0,0.05), 0 4px 12px rgba(0,0,0,0.03)`.
- **Surface Elevation Level 2 (Modals, Popovers, & Dropdown Menus)**: Elevated `#FFFFFF` surfaces layered with `box-shadow: 0 8px 24px rgba(15, 23, 42, 0.08), 0 2px 6px rgba(15, 23, 42, 0.04)` and `border: 1px solid #CBD5E1`.
- **Focus Rings**: Two-tone accessibility focus outlines using `0 0 0 2px #FFFFFF, 0 0 0 4px #0F766E` to ensure effortless keyboard navigation.

## Shapes

The interface embraces a friendly, rounded aesthetic inspired by modern desktop guidelines:
- **Base Components (Inputs, Buttons, Badges, Tabs):** 8px (`0.5rem`) corner radius.
- **Cards, Panels, and Dialogue Windows:** 12px (`0.75rem`) to 16px (`1rem`) corner radius.
- **Pill Badges & Status Chips:** Full pill radius (`9999px`) for safe/blocked badges and protection indicators.
- **Checkboxes & Segment Toggles:** 4px to 6px rounded edges for tactile, friendly control points.

## Components

### Buttons
- **Primary:** Background `#0F766E`, text `#FFFFFF`, height 40px, padding `0 20px`, corner radius 8px. Hover state shifts to `#115E59`; active state shifts to `#134E4A`.
- **Secondary / Soft:** Background `#F1F5F9`, text `#334155`, border `1px solid #E2E8F0`. Hover state transitions to `#E2E8F0`.
- **Destructive / Remove:** Subtle light red tint (`#FEF2F2`), text `#B91C1C`, hover `#FEE2E2`.

### Inputs & URL Entry Field
- Height 42px, background `#FFFFFF`, border `1px solid #CBD5E1`, text `#1E293B`, placeholder `#94A3B8`.
- Integrated leading prefix container (e.g., `https://` lock icon) in `#F8FAFC` to assist parents in entering clean domain names.
- Focus state provides a clean teal border `#0F766E` and crisp halo.

### Progress Step Indicators
- Horizontal step rail with numbered or icon circular nodes (32px diameter).
- **Active Step:** `#0F766E` filled circle, white text, bold label below.
- **Completed Step:** `#ECFDF5` background, `#10B981` border with emerald checkmark icon.
- **Pending Step:** `#F1F5F9` background, `#94A3B8` text, connecting bar in `#E2E8F0`.

### Desktop Allowlist Cards & Items
- Structured list cards with 12px padding, dividing lines in `#F1F5F9`.
- Domain label in `headline-sm`, subtitle showing rule type (e.g., "全校園模式允許", "自訂新增").
- Trailing quick actions: Status Pill ("已允許" in `#ECFDF5` / `#0F766E`), individual switch toggle, and soft icon-only delete trigger.

### Toggle Switch
- Desktop standard 44px width x 24px height track.
- Off state: Track `#CBD5E1`, circular thumb `#FFFFFF` (18px).
- On state: Track `#0F766E`, circular thumb `#FFFFFF` shifted smoothly with spring easing.

### Protection Shield Banner
- Accent banner at top of main view: light mint `#ECFDF5` or light slate blue `#EFF6FF` container with 1px border.
- Houses protection status indicator ("目前防護狀態：已啟用白名單管制") paired with quick parent verification timestamp.