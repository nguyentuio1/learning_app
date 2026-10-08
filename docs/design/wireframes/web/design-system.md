# LA-75 — low-fidelity wireframe conventions

## Purpose

This edition is a structural sketch for reviewing layout, navigation, fields, actions and business-state handling. It does not choose a final visual identity or imply that a website has been implemented.

## Visual conventions

- White canvas, light-grey grouping, neutral borders and square boxes.
- Grey navigation/filter fill indicates selection; a slightly darker outlined button indicates the main action, not a brand colour.
- All status tags use the same neutral treatment with complete status text.
- Shared sidebar/header/content grid across actors.
- Asymmetric two-column login: system name and a minimal route motif on the left; Email, Password and Sign in on the right. No slogans, extra login instructions, gradients, shadows or coloured accents. The motif is a layout assumption, not a live map or flight-control feature.
- Maps and time-series charts are crossed placeholder boxes. Other chart bars illustrate arrangement only, not actual quantities.

## Content conventions

Keep interface vocabulary and requirements: screen/navigation/field names, action labels, required markers, validation hints, the five fixed roles and existing service names.

Keep exact delivery statuses: PENDING, APPROVED, READY_FOR_TAKEOFF, IN_TRANSIT, ARRIVED, DELIVERED, CANCELLED and FAILED.

Omit repeated page/card descriptions, duplicated role text and long explanatory paragraphs from every screen. Keep useful record context, form validation hints, short conflict warnings and destructive-action consequences. Scope notes and source references belong in index.md, not permanent UI panels.

Use bracketed placeholders for data:
- `[Order ID]`, `[Drone ID]`, `[Station ID]`, `[User name]`, `[Customer]`.
- `[Origin]`, `[Destination]`, `[Date]`, `[Time]`, `[n]`, `[%]`, `[Value]`.
- `[Type]`, `[Weight]`, `[Dimensions]`, `[Email]`, `[Phone]`.
- `[AI-generated delivery summary]` instead of a realistic generated narrative.

Placeholders identify the content type. They are not proposed API responses, test records or final microcopy.

## Deliverables and boundaries

- 20 screens across Dispatcher, Logistics Manager and System Administrator.
- Three editable multi-page Draw.io files, individual SVG/PNG files and three overview sheets.
- index.md preserves the source-document basis, unresolved assumptions and screen inventory.
- Customer web is required by the supplied Jira screenshot but remains undesigned pending user-flow confirmation.
- Manager stays read-only; no drone flight controls or new business features.
- The reviewed Web layout is included under `docs/design/wireframes/web/`; unresolved requirement decisions remain in index.md. Mobile wireframes are outside this task's scope.
- This package contains documentation only. Downloaded design tools, generator scripts, experimental assets and application implementation are excluded.

## Verification limits

Static checks cover grayscale colours, text widths/heights, text overlaps, canvas bounds, valid XML/SVG and index links, plus visual inspection of actor overviews.

These are fixed 1440 × 900 wireframes, not a responsive or interactive website. Keyboard navigation, ARIA, live telemetry, map integration and runtime behaviour are not implemented or tested.
