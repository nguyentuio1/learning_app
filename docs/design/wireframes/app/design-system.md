# LA-76 — Customer and Station Operator low-fidelity wireframe conventions

## Purpose

This edition is a structural sketch for reviewing layout, navigation, fields, actions and business-state handling across the Customer and Station Operator mobile apps. It does not choose a final visual identity or imply that the apps, backend services or integrations have been implemented.

## Visual conventions

- White canvas, light-grey grouping, neutral borders and square boxes.
- Grey navigation/filter fill indicates selection; a slightly darker outlined button indicates the main action, not a brand colour.
- All status tags use the same neutral treatment with complete status text.
- Keep shared spacing, field alignment, button treatment and repeated components consistent across Customer and Station Operator screens.
- Use the navigation pattern shown in the source frames. Customer navigation uses the four tabs Home, Orders, Packages and Profile. Do not invent additional tabs.
- Use crossed placeholder boxes for maps and time-series charts. They are layout assumptions, not live maps, telemetry feeds or flight-control features.
- Neutral placeholders may represent avatars, package photos and other media when the source does not provide a required asset.
- Keep validation, empty, loading and unavailable-data states distinguishable through text and neutral outlines, not colour alone.

## Content conventions

Keep interface vocabulary and requirements: screen/navigation/field names, action labels, required markers, validation hints and existing service names. Preserve exact source wording where it is legible in the supplied screen references.

Keep exact delivery statuses: PENDING, APPROVED, READY_FOR_TAKEOFF, IN_TRANSIT, ARRIVED, DELIVERED, CANCELLED and FAILED.

Omit repeated page/card descriptions, duplicated role text and long explanatory paragraphs from every screen. Keep useful record context, form validation hints, short conflict warnings and destructive-action consequences. Put scope notes, source references and unresolved assumptions in the relevant index.md, not permanent UI panels.

Use bracketed placeholders for data:
- `[Order ID]`, `[Tracking number]`, `[Package ID]`, `[Drone ID]`, `[Station ID]`.
- `[User name]`, `[Customer]`, `[Recipient name]`.
- `[Origin]`, `[Destination]`, `[Date]`, `[Time]`, `[ETA]`.
- `[Type]`, `[Weight]`, `[Dimensions]`, `[Description]`.
- `[Email]`, `[Phone]`, `[Address]`, `[Pickup Code]`.
- `[AI-generated delivery summary]`, `[AI response based on customer data]`.
- `[n]`, `[%]`, `[Value]`.

Placeholders identify the content type. They are not proposed API responses, test records or final microcopy. Do not invent realistic personal data, tracking numbers, telemetry values or AI-generated narratives.

## Customer app conventions

- The Customer screen inventory contains 27 screens, C01–C27, covering authentication, home, orders, delivery creation, package and address management, notifications, AI Assistant and profile settings.
- Preserve the four navigation tabs: Home, Orders, Packages and Profile.
- Keep Create Delivery as the primary action from Home and Add Package available from Packages where shown in the source.
- Preserve the documented delivery-creation sequence: Create Delivery → Review Delivery → Order Submitted. Include validation/error states and package/address selection where shown.
- Order details should show only the context and actions supported by the relevant screen and order state. Do not imply that every action is available for every status.
- Preserve the documented status filters and exact system delivery statuses. User-facing labels may use the wording shown in the source frames.
- Show tracking maps, live telemetry and time-series content as crossed placeholders unless the deliverable specifically requires a non-functional structural sketch. Do not present sample values as real telemetry.
- The documented package constraint is a maximum weight of 3 kg. The documented creation validation includes origin and destination stations being different, recipient details and phone validation. Keep these rules only where supported by the source.
- Address management, notification read states, theme selection and AI Assistant should use the fields and actions shown in their source screens.
- The pickup-code flow is represented by the source screens C08 and C11–C13. Use `[Pickup Code]` rather than a real code; do not imply secure-code delivery or backend validation has been implemented.

## Station Operator app conventions

- The Station Operator screen inventory contains 16 screens, O01–O16, covering sign-in, dashboard, pickup and arrival tasks, scanning, safety checks, slot and station management, exception reporting, notifications and profile/station information.
- Preserve the source-supported functions: manage landing stations and slots, monitor station availability, confirm package pickup and confirm drone arrival.
- Use placeholders such as `[Station ID]`, `[Slot]`, `[Order ID]`, `[Tracking number]` and `[Drone ID]` for operational records.
- At pickup confirmation, show the three documented checklist items: matching package description/photo, weight at or below 3 kg, and intact packaging. The Confirm action remains disabled until all three items are checked, as specified in the supplied summary.
- Represent scan failures and orders that do not match the assigned station or eligible state with a concise validation/error state. Do not invent additional failure categories.
- Arrival confirmation may show crossed placeholders for the route/map and telemetry. The sensor state `Occupied` is shown as source-described context, not proof of a live sensor integration.
- Keep station states (Operational, Maintenance, Offline), slot states and delivery statuses distinct. The manual slot override and maintenance flows should include the source-described note/reason fields where shown.
- The maintenance flow is described as preventing selection of the affected station in Customer delivery creation. Treat this as a documented intended behaviour, not proof that cross-app synchronisation is implemented.
- Exception reporting should allow the source-described issue details and photo attachment, with Dispatcher as the reported recipient.
- Do not add drone piloting, route-control, take-off or other flight-hardware controls. The platform scope is delivery operations, not drone flight control.

## Deliverables and boundaries

- Customer mobile wireframes cover the 27-screen inventory C01–C27; Station Operator mobile wireframes cover the 16-screen inventory O01–O16, subject to confirmation against the original source frames.
- Provide editable multi-page Draw.io source files and individual SVG/PNG exports, plus overview sheets, if required by the applicable task.
- The relevant index.md preserves the source-document basis, unresolved assumptions and screen inventory.
- This document covers Customer and Station Operator mobile apps. Customer web and Station Operator web screens are outside this mobile scope unless separately requested and confirmed.
- Do not treat a screen inventory or wireframe as proof that authentication, APIs, backend workflows, push notifications, live tracking, sensor integrations or AI services are implemented.
- No new business features, undocumented state transitions or drone flight controls.
- This package contains design documentation only. Application implementation and runtime integration are excluded.

## Verification limits

Static checks cover grayscale colours, text widths/heights, text overlaps, canvas bounds, valid Draw.io XML/SVG and index links, plus visual inspection of Customer and Station Operator overviews.

These are fixed-size structural wireframes, not responsive or interactive applications. Keyboard navigation, ARIA, live telemetry, map integration, API behaviour, push delivery, sensor behaviour and runtime workflows are not implemented or tested by the wireframes.

## Source basis and unresolved assumptions

- **Customer screen inventory:** the supplied summary of Customer screens C01–C27.
- **Station Operator screen inventory:** the supplied summary of Station Operator screens O01–O16.
- **Project requirements:** use `SRS.md` and `use-cases.md` to verify actor responsibilities, supported use cases and delivery statuses before finalising the wireframes.
- Confirm the exact screen contents and labels against the original source images/Figma frames; the summaries alone do not verify every visual detail.
- Confirm the exact status transition after pickup confirmation and the ownership/validation of Pickup Code against the project requirements.
- Confirm whether station/slot state labels and telemetry fields match the source requirements. Do not infer additional states or real-time integrations from placeholder screens.
