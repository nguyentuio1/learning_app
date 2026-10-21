# LA-79 — Authentication verification

**Scope:** Unit/integration tests and acceptance verification for LA-15, SRS FR-01/02 and authentication audit events (FR-32).

**Base:** develop `609f609`. Existing tests remain attributed to their original authors; they are reused, not copied or rewritten.

**Result:** Release run on 2026-10-21: 123 passed, 0 failed, 0 skipped (107 baseline cases + 16 new cases).

## Acceptance mapping

| Criterion | Executable evidence |
|---|---|
| Customer registration; stored hash; duplicate email/phone rejected | Existing `AuthApiTests.Register_*` HTTP cases |
| Required fields, invalid email, weak password and invalid phone return 400 with field errors | Existing `Register_rejects_invalid_input_with_400_and_field_errors` / `Register_requires_all_fields`; new overlong HTTP and boundary validation cases |
| Correct login issues JWT/refresh tokens; wrong credentials return indistinguishable 401; inactive/suspended users get 403 | Existing `Login_*`; new `Blank_login_fields_return_field_specific_errors` |
| Access/refresh lifetimes, refresh rotation/replay/expiry, logout and token ownership | Existing `Refresh_*`, `Logout_*`; new configurable lifetime and random refresh-token checks |
| Public registration cannot grant staff access or inject state/hash | New `Public_registration_ignores_injected_staff_role_status_and_password_hash` verifies response and persisted entity |
| Invalid registration does not partially persist users, refresh tokens or audit records | New `Overlong_register_fields_return_400_without_persisting_any_identity` |
| Password hashing verifies only the correct password and uses salts | New `Hashes_are_salted_and_verify_only_the_matching_password` invokes the production hasher |
| Sensitive auth actions are auditable | Existing login/logout audit HTTP cases |

## Execution and limits

From repository root:

```sh
dotnet test backend/SmartDroneDelivery.Tests/SmartDroneDelivery.Tests.csproj -c Release
dotnet test backend/SmartDroneDelivery.Tests/SmartDroneDelivery.Tests.csproj -c Release --filter FullyQualifiedName~AuthenticationBoundaryTests
```

Source: [new cases](../../backend/SmartDroneDelivery.Tests/AuthenticationBoundaryTests.cs), [existing HTTP cases](../../backend/SmartDroneDelivery.Tests/AuthApiTests.cs), [API contract](../api/auth-api.md).

HTTP tests exercise the real ASP.NET pipeline through the existing `AuthApiFactory`; persistence uses its isolated EF InMemory database. This is not a PostgreSQL concurrency/load test, mobile/web end-to-end run, or proof of production deployment. No authentication implementation, shared factory, dependencies or existing tests are changed. No unapproved rule requiring login passwords to pass registration-strength validation is introduced: missing login values are 400, incorrect supplied credentials are 401.
