# ADR 0003 — Compile-time DTO mapping

- Status: Accepted
- Date: 2026-07-15

## Context

RiposteOS must never expose domain entities directly through HTTP contracts. Repeated
entity-to-DTO mappings should be centralized, validated during the build and kept out
of endpoints.

AutoMapper was considered because its profile model is familiar to the team. However,
versions 15 and later use a conditional commercial license, while the last generally
available MIT version is affected by a high-severity denial-of-service advisory. Both
options conflict with the project's open-source, self-hosted and dependency-audit
requirements.

## Decision

Use the latest stable Apache-2.0 release of Mapperly for API DTO mappings.

- DTOs live in `RiposteOS.Api/<Module>/Dtos`.
- Mapperly partial mappers live in `RiposteOS.Api/<Module>/Mappers`.
- Required target members are checked at compilation.
- Security-sensitive mappings remain explicit.
- Domain entities are never serialized directly.

## Consequences

Mappings are generated without runtime reflection, runtime registration or a license
key. Mapping errors fail the warning-as-error build. The code uses feature-specific
static mapper methods instead of injecting a generic `IMapper` service.

This decision can be revisited if AutoMapper returns to an OSI-approved license with a
maintained, vulnerability-free release, or if RiposteOS explicitly changes its core
licensing constraints.
