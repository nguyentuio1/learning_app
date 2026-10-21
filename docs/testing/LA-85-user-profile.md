# LA-85 — User profile verification

**Scope:** Unit/integration tests and acceptance verification for LA-17 / SRS FR-04.

**Base:** develop `609f609`. Existing `ProfileApiTests`, factory and avatar fake remain unchanged, with their original authorship preserved.

**Result:** Release run on 2026-10-21: 119 passed, 0 failed, 0 skipped (107 baseline cases + 12 new cases).

## Acceptance mapping

| Criterion | Executable evidence |
|---|---|
| View own profile; name/phone changes persist; email/role cannot be edited | Existing get/update HTTP tests |
| Another user's ID cannot redirect an own-profile update | New `Injected_user_id_cannot_update_another_accounts_profile` verifies both accounts |
| Invalid/duplicate phone and excessive/blank name rejected with field errors | Existing profile validation/409 tests; new boundary DataAnnotations tests and no-partial-save HTTP case |
| Missing token denied and inactive/suspended accounts blocked | Existing `Every_profile_endpoint_requires_a_token`, inactive GET; new suspended GET/PUT/password/avatar matrix |
| Password change verifies current password, rejects unchanged/weak password, hashes new password, records audit and revokes only that user's refresh tokens | Existing `Change_password_*` tests |
| Wrong current password does not destroy session or create a change audit | New `Wrong_current_password_keeps_refresh_session_usable_and_creates_no_change_audit` verifies refresh through HTTP |
| Both current/new password fields required | New `Password_change_requires_both_fields_with_actionable_messages` executes validators |
| JPEG/PNG avatar, 5 MiB boundary, content signature, replacement/cleanup and persistence | Existing upload HTTP tests with deterministic fake storage |
| Wrong multipart field rejected without storage write | New `Wrong_multipart_field_is_rejected_without_storage_write` |
| Failed upload preserves previous avatar | Existing `A_failed_upload_leaves_the_existing_avatar_untouched` |

## Execution and limits

```sh
dotnet test backend/SmartDroneDelivery.Tests/SmartDroneDelivery.Tests.csproj -c Release
dotnet test backend/SmartDroneDelivery.Tests/SmartDroneDelivery.Tests.csproj -c Release --filter FullyQualifiedName~ProfileBoundaryTests
```

Source: [new cases](../../backend/SmartDroneDelivery.Tests/ProfileBoundaryTests.cs), [existing cases](../../backend/SmartDroneDelivery.Tests/ProfileApiTests.cs), [API contract](../api/auth-api.md).

HTTP integration uses the actual ASP.NET pipeline, isolated EF InMemory storage and existing `FakeAvatarStorage`. This verifies the avatar service contract and persisted URL, not live MinIO/network availability or mobile/web presentation. No production profile code or existing tests are changed. Client success feedback and session-navigation behavior belong to web/mobile tasks, not this backend test suite.
