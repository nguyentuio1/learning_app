# LA-81 — Role-based access verification

**Scope:** Unit/integration tests and acceptance verification for LA-16 / SRS FR-03.

**Base:** develop `609f609`. Original `RbacTests`, probe controller and factory remain unchanged and retain their original authorship.

**Result:** Release run on 2026-10-21: 120 passed, 0 failed, 0 skipped (107 baseline cases + 13 new cases).

## Acceptance mapping

| Criterion | Executable evidence |
|---|---|
| Five role codes match seeded roles | Existing `App_role_codes_match_the_seeded_roles` |
| Missing token yields 401; anonymous endpoint stays open; no attribute is protected by default | Existing `Request_without_a_token_gets_401_with_a_problem_body`, `Anonymous_endpoints_stay_open`, `Endpoint_without_any_attribute_is_protected_by_default` |
| Valid token with disallowed role yields 403 | Existing 5×5 `Each_role_can_only_call_its_own_endpoint` matrix and exact problem-body assertion |
| Endpoints may accept several roles; authenticated-only endpoint accepts all five | Existing multi-role and signed-in HTTP cases |
| Expired, malformed, wrong key/issuer/audience tokens are rejected | Existing JWT HTTP cases |
| Missing/wrong claim name, lowercase role and comma-injected role do not become ADMIN | New `Valid_signature_without_exact_admin_claim_is_authenticated_but_forbidden`: signed-in 200, admin 403 |
| Future not-before, unsigned token and wrong authorization scheme rejected | New three 401 HTTP cases |
| Custom headers/query cannot override signed role | New `Query_and_custom_headers_cannot_override_the_signed_customer_role` |
| Unit-level role requirement evaluates exact role membership | New `Role_handler_matches_exact_role_codes_not_case_or_prefix` executes the real authorization handler, not reflection |
| Role changes appear in renewed tokens | Existing `Role_change_takes_effect_after_the_next_refresh` |

## Execution and limits

```sh
dotnet test backend/SmartDroneDelivery.Tests/SmartDroneDelivery.Tests.csproj -c Release
dotnet test backend/SmartDroneDelivery.Tests/SmartDroneDelivery.Tests.csproj -c Release --filter FullyQualifiedName~RbacBoundaryTests
```

Source: [new cases](../../backend/SmartDroneDelivery.Tests/RbacBoundaryTests.cs), [existing cases](../../backend/SmartDroneDelivery.Tests/RbacTests.cs), [contract](../api/auth-api.md).

The existing test-only probe controller exercises actual authentication/authorization middleware without adding production endpoints. These checks do not claim coverage of future business controllers or UI route guards. LA-92 supplies its own HTTP admin-controller tests and an additional current-database-role check; that exception must not be confused with the general access-token role policy. No shared test configuration, production RBAC or existing source is changed.
