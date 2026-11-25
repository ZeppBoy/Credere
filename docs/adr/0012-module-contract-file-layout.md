# ADR-0012: File layout for public module contracts

**Status:** Accepted 2026-11-25. Amends the folder layout in [ADR-0008](0008-module-public-contracts.md) as approved on [M2-C2](https://github.com/ZeppBoy/Credere/issues/66#issuecomment-7166693524).

## Context

The M2 contract task places the Origination and Servicing interfaces at their project roots and gives types shared by several feature slices an owning `Contracts/` directory. ADR-0008 and the architecture diagram placed the interfaces under `Application/`, and the folder allow-list did not account for public types shared by slices. Existing consumers already use the root namespaces.

## Decision

1. The Origination and Servicing facades live in `IOriginationModule.cs` and `IServicingModule.cs` at their respective module roots, in the existing module namespaces. The `AddXModule` extension remains the module's DI entry point.
2. Public IDs, enums, value types, views, and module errors used by multiple slices live in the owning module's `Contracts/` directory. Slice-specific command, query, result and view types live with their feature under `Application/Commands/` or `Application/Queries/`. Publishers keep their public integration events under `IntegrationEvents/`.
3. Each module exposes one facade. Aggregates, infrastructure, handlers and domain events stay internal. The existing dependency rules and the host translation decision in [ADR-0011](0011-m2-module-contracts.md) continue to apply.

## Consequences

Hosts keep importing the same facade namespaces. The location of a DTO shows whether it belongs to one feature or is shared across features in its module. The existing architecture tests continue to enforce module isolation.
