# Proofreading, Formatting, and Terminology Checklist (LA-255)

Owner: [LA-255](https://hungndemail.atlassian.net/browse/LA-255), Hung Nguyen. Parent story: [LA-204](https://hungndemail.atlassian.net/browse/LA-204) (TBA Review retains final report consolidation, export, and approval).

---

## 1. Cross-document structure and numbering

| Report part | Repository source | Confluence target | Formatting & numbering status |
|---|---|---|---|
| **I. Project Introduction** | [01-project-introduction.md](01-project-introduction.md) | [Page 14188546](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=14188546) | Numbered `1.`–`6.` with sentence-case headings (`Checked`). |
| **II. Project Management Plan** | [02-project-management-plan.md](02-project-management-plan.md) | [Page 11927566](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11927566) | Numbered `1.`–`8.` with sentence-case headings (`Checked`). |
| **III. Software Requirements Specification** | [SRS.md](../requirements/SRS.md) | Consolidated via LA-253 | Cross-referenced from Chapters I, II, and IV (`Checked`). |
| **IV. Software Design Description** | [04-software-design-description.md](04-software-design-description.md) | [Page 11993109](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11993109) | Numbered `1.`–`4.` (`4.3 Historical references`), sentence-case headings aligned with Chapters I–II (`Checked`). |
| **V. Test Plan & Test Report** | [test-plan.md](../testing/test-plan.md) · [test-report.md](../testing/test-report.md) | [Chapter V](https://hungndemail.atlassian.net/wiki/pages/viewpage.action?pageId=11927583) | Linked from Chapter II and Chapter IV (`Checked`). |
| **VI. Conclusion & Final Export** | Assembled in Confluence / export | Consolidated under LA-204 | Pending final export & TOC check by TBA Review. |

Formatting fixes applied in this pass:
- Fixed missing spaces and added direct Confluence page links in [README.md](README.md) (`14188546`, `11927566`), and updated the chapter file count wording.
- Harmonized [04-software-design-description.md](04-software-design-description.md) headings to sentence case (`1. System design`, `2. Database design`, `3. Detailed design`, `4. Handoff and review`, `4.3 Historical references`) to match Chapters I and II.

---

## 2. Mobile and station terminology consistency

All repository report chapters and Sprint 7 mobile/station user-flow sources should use the following canonical terms:

| Term / Symbol | Canonical usage | Notes / verified alignment |
|---|---|---|
| **Flutter Customer app** | `Customer app` (`AppFlavor.customer`) | Owns customer orders, tracking, delivery confirmation, ETA, and AI assistant (`LA-160`, `LA-167`, `LA-169`, `LA-171`). |
| **Flutter Station Operator app** | `Station Operator app` (`AppFlavor.operator`) | Owns station availability, slot maintenance, origin intake, destination pickup, expired-parcel clearing, and operator notifications (`LA-170`). |
| **Landing station & slot states** | `Landing station` with `EMPTY`, `OCCUPIED`, `RESERVED`, `ERROR` slots | Manual operator toggle is restricted to `EMPTY <-> ERROR`. |
| **Order lifecycle transitions** | `APPROVED -> READY_FOR_TAKEOFF` (intake, requires `PLANNED` mission), `APPROVED -> FAILED` (intake rejection), `IN_AIR -> ARRIVED` (station arrival), `ARRIVED -> DELIVERED` (recipient/pickup confirmation) | Do not use non-backend labels (`IN_ORIGIN_STATION`, `REJECTED`, `COMPLETED`) in flow docs or API examples. |

---

## 3. Diagrams and links check

- **Relative links in `docs/report/*.md`:** Audited against the `origin/develop` tree; 0 broken relative links.
- **Mermaid diagrams in `docs/design/user-flows/*.md` and `docs/report/*.md`:** All `mermaid` code fences are properly closed and syntactically balanced.
- **AI / release-gate disclaimer consistency:** Chapters I, II, and IV consistently state that default-off AI adapters, provider/model activation, chat retention, physical-device signing, and hardware flight control remain outside automated test/merge claims.

---

## 4. Proofreader sign-off and Confluence export evidence (LA-204 gate)

> [!IMPORTANT]
> Per **LA-204** and **LA-255**, repository commits alone do not establish Confluence publication or final report acceptance. Record two distinct human proofreaders and the published Confluence page versions / final export link below before closing LA-204.

| Gate item | Owner / Reviewer | Evidence / Confluence page & version | Status |
|---|---|---|---|
| Cross-document formatting & terminology pass | Hung Nguyen (`LA-255`) | This checklist + PR on `docs/LA-255-proofread-format-report` | Prepared for review |
| Proofreader #1 sign-off | *Pending human reviewer 1* | *Record reviewer name, date, and notes* | Pending |
| Proofreader #2 sign-off | *Pending human reviewer 2* | *Record reviewer name, date, and notes* | Pending |
| Confluence publication & final export | TBA Review (`LA-204`) | *Record exported PDF/DOCX + Confluence page versions (`14188546`, `11927566`, `11993109`, Chapter V/VI)* | Pending |
