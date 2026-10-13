# SmartDroneDelivery — Design System
**Task:** LA-77 — Define design system  
**Parent task:** LA-14 — Design UI/UX for the web portal and mobile apps  
**Scope owner:** Shared visual and interaction rules for the Customer App and Station Operator App  
**Status:** Draft — pending validation against the source Figma file

**Source reviewed:** `SmartDroneDelivery — Customer App.pdf` (12 pages); Operator screen inventory available in project context.

## 1. Objective and scope
Define the visual foundations and reusable components shared by the two mobile apps:
- Customer App
- Station Operator App

LA-75 web wireframes are owned by the frontend web workstream. This document does not deliver web wireframes. The team may reuse agreed design tokens on web, but web-specific layouts and navigation remain outside this mobile task.

The supplied Customer PDF supports documenting screen patterns and component types, but it does **not** establish exact hex colors, font family, font sizes, spacing values, radii, shadows, or icon-library specifications. Verify those values in Figma before calling this specification final.

## 2. Design principles
- **Clear status visibility:** users should be able to scan order and task statuses quickly.
- **Task-focused screens:** emphasize the primary action for the current screen.
- **Consistent components:** equivalent controls should behave and look consistently across both mobile apps.
- **Role-appropriate workflows:** customer delivery-request tasks must remain distinct from station operator package-handling tasks.
- **Review before consequential submission:** customer delivery details should be reviewable before request submission.
- **Honest data states:** label sample content and unavailable values rather than presenting them as live facts.
- **Accessible interaction:** use clear labels, sufficient contrast, visible focus/selection states, and text in addition to color to convey status.
- **Predictable navigation:** preserve each app's established bottom-navigation destinations.

## 3. Visual tokens to confirm in Figma

| Token group | Token / specification | Value |
|---|---|---|
| Color | Primary / brand | TBD — inspect Figma styles |
| Color | Secondary | TBD — inspect Figma styles |
| Color | Page background / surface | TBD — inspect Figma styles |
| Color | Primary / secondary text | TBD — inspect Figma styles |
| Color | Border / divider | TBD — inspect Figma styles |
| Color | Success / warning / error / info | TBD — inspect Figma styles |
| Typography | Font family and weights | TBD — inspect Figma styles |
| Typography | Heading scale | TBD — inspect Figma styles |
| Typography | Body / label / caption scale | TBD — inspect Figma styles |
| Spacing | Base unit and spacing scale | TBD — inspect Figma styles |
| Shape | Border-radius scale | TBD — inspect Figma styles |
| Elevation | Shadow / elevation rules | TBD — inspect Figma styles |
| Iconography | Icon family, size, and stroke rules | TBD — inspect Figma styles |
| Motion | Transition/duration rules, if used | TBD — inspect Figma/prototype |

**Rule:** do not invent exact token values based on the PDF text extraction. Copy the values from Figma styles/components and record them here.

## 4. Reusable component inventory

| Component | Customer App usage | Station Operator usage | States / rules to define |
|---|---|---|---|
| Primary button | Sign in, Create Delivery, Save Package, Submit Delivery Request | Login and primary task/receipt actions | Default, pressed, disabled, loading where applicable |
| Secondary / text button | Cancel, Back to Edit, Edit, Delete, View Order History | Secondary task and navigation actions | Consistent hierarchy and disabled behavior |
| Text input | Account, package, recipient, search fields | Login and task-related fields where present | Empty, focused, filled, validation error, disabled |
| Password input | Sign In | Operator Login | Masked value and validation feedback |
| Search field | Orders, packages, addresses | Package Tasks if search is present in Figma | Empty, query entered, no results, loading if data-backed |
| Filter chips / tabs | Order status, package type, notification filters | Task filters only if shown in Figma | Selected/unselected states and clear labels |
| Status badge / label | Pending, Approved, Scheduled, In Transit, Delivered; Fragile/Non-fragile | Task/package/station statuses shown in Figma | Use text as well as color; define status mapping |
| Card / list item | Orders, packages, addresses, notifications | Package tasks, station/task summaries | Pressed/selected states where interactive |
| Timeline / progress indicator | Order Details tracking | Task progression only if present in Figma | Completed/current/pending; unavailable times explicit |
| Form section | Package, route, recipient, profile data | Login/task/receipt forms where present | Clear headings, labels, required/optional fields |
| Empty state | No recent deliveries, no orders/packages/addresses | No assigned tasks or no slot/task data if designed | Explanatory text and relevant next action |
| Bottom navigation | Home, Orders, Packages, Profile | Home, Tasks, Station, Profile | Active/inactive states and correct destinations |
| Dialog / confirmation | Delete/sign-out or other destructive actions if designed | Receipt/task confirmation if designed | Clear confirm/cancel actions; prevent accidental action |
| AI chat message | AI Assistant | Not applicable unless the Operator Figma explicitly includes it | Distinguish user/assistant messages; label examples |
| Station/slot indicator | Not applicable unless reused elsewhere | Station Details and Station Slots | Meaning and available states must match actual Figma/business rules |

This is a component inventory to validate, not a claim that every component already exists as a Figma component. Reuse components where the same behavior is required; do not force unrelated role-specific controls into one component.

## 5. Interaction and content rules
- Every input should have a visible label; placeholders supplement labels rather than replacing them.
- Mark optional fields clearly, such as package description, photo, or delivery instructions when applicable.
- Use consistent wording for the same action across both apps.
- Make the primary action visually prominent without making destructive actions look primary.
- Customer review screen should let the user check and edit package, route, and recipient information before submitting.
- Operator receipt confirmation must communicate that it confirms arrival at the origin station; it must not be labeled as final recipient delivery confirmation.
- Use explicit labels such as `Date unavailable`, `Time unavailable`, or `Not available` where data is intentionally unavailable.
- Keep sample orders, notifications, addresses, and AI conversation content distinguishable from real data.
- Define validation, loading, empty, success, and error states for data-driven controls when relevant.
- Status must not be conveyed by color alone; include text and, where appropriate, icons.

## 6. Consistency across the two mobile apps
- Use the same confirmed color, typography, spacing, shape, icon, and elevation tokens in both apps.
- Keep common controls (buttons, fields, cards, status labels) visually consistent while allowing role-specific content and actions.
- Preserve separate bottom navigation:
  - Customer: Home, Orders, Packages, Profile.
  - Station Operator: Home, Tasks, Station, Profile.
- Do not reuse Customer navigation labels in the Operator app when the operator workflow uses different destinations.
- Check text wrapping, scrolling, keyboard behavior, touch targets, contrast, and focus/selection states on target mobile sizes.
- If the team extends the system to web, web may reuse semantic tokens and components, but desktop layout/navigation decisions belong to LA-75/frontend web.

## 7. Finalization checklist
- [ ] Confirm color styles and exact values in Figma.
- [ ] Confirm font family, weights, and type scale.
- [ ] Confirm spacing scale, radii, borders, shadows, and icon rules.
- [ ] Identify which controls are actual reusable Figma components and define variants.
- [ ] Confirm button, field, status, card, filter, navigation, dialog, and empty-state variants.
- [ ] Check consistency across Customer and Station Operator screens.
- [ ] Verify the Operator receipt-confirmation wording and status meaning.
- [ ] Add Figma source URL and component-library location.
- [ ] Review accessibility and responsive behavior for both mobile apps.
- [ ] Keep web wireframe deliverables under LA-75 rather than this task.

## 8. Source limitations
The supplied Customer App PDF has 12 Customer screens. It does not provide the Operator screen visuals, exact visual token values, or a complete component-library specification. Operator details and exact token values must be checked in the corresponding Figma frames. Do not treat the TBD token rows as finalized design decisions.
