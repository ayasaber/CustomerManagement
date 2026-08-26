# security-administration — plan overview

Entry point for the **security-administration** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 06 | [06-story-identity-auth-foundation.md](06-story-identity-auth-foundation.md) | Identity Auth Foundation | none | 05 (customer-management) |
| 07 | [07-story-user-lifecycle-and-role-assignment.md](07-story-user-lifecycle-and-role-assignment.md) | User Lifecycle And Role Assignment | none | 06 |
| 08 | [08-story-permission-model-and-policy-administration.md](08-story-permission-model-and-policy-administration.md) | Permission Model And Policy Administration | none | 06, 07 |
| 09 | [09-story-audit-log-capture-and-query.md](09-story-audit-log-capture-and-query.md) | Audit Log Capture And Query | none | 06, 07, 08 |
| 10 | [10-story-system-settings-api-and-policy-enforcement.md](10-story-system-settings-api-and-policy-enforcement.md) | System Settings API And Policy Enforcement | none | 06, 08, 09 |
| 11 | [11-story-gateway-auth-forwarding-and-policy-consumption.md](11-story-gateway-auth-forwarding-and-policy-consumption.md) | Gateway Auth Forwarding And Policy Consumption | none | 06, 08, 10 |
| 12 | [12-story-admin-console-and-public-registration-ui.md](12-story-admin-console-and-public-registration-ui.md) | Admin Console And Public Registration UI | none | 06, 07, 08, 09, 10, 11 |

## Dependency notes

- Story 06 is the identity pivot point and introduces ASP.NET Core Identity plus token issuance.
- Story 07 and Story 08 split account lifecycle from permission model to keep API reviews manageable.
- Story 09 depends on prior auth and admin actions so audit capture can include actor identity and action sources.
- Story 10 applies mutable security settings only after audit capture exists.
- Story 11 hardens gateway trust boundaries after JWT contracts are stable.
- Story 12 consumes all prior backend contracts and delivers admin + registration UX.
- Customer-management stories 01-05 remain functional during transition but are expected to move from placeholder `AgentOnly` role checks to permission-based authorization in this feature sequence.
