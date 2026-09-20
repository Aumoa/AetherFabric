# Aether Development Guidelines

This file defines the general design, implementation, and verification principles that AI agents and developers must follow when working in the Aether repository.

This document does not establish a specific feature, product roadmap, technology choice, or short-term implementation goal. Each task request determines its concrete goal and scope. Do not treat undocumented or unverified assumptions as settled decisions; present relevant use cases and alternatives first.

## 1. Project Perspective

- Aether is intended to be a general-purpose server platform for persistent online worlds and real-time distributed simulations.
- Do not embed the rules or domain model of a particular game into the core.
- Prioritize public APIs and developer experience over low-level infrastructure details.
- Development must remain straightforward in a single process without preventing later decomposition across processes and nodes.
- Evaluate new designs for long-term maintainability, replaceability, and operability.

## 2. Core Design Principles

### Server Authority

The server owns the authoritative world state and outcomes. Treat clients and external inputs as untrusted data that must be validated rather than accepting transmitted state at face value.

### Distribution-Aware Design

Design boundaries so that the distinction between local and remote calls does not become a long-term liability. Do not introduce distributed-system complexity before a concrete need exists.

### Modularity

Each module must have one clear responsibility. Keep dependency direction from unnecessarily coupling higher-level capabilities to lower-level implementations. Modules should be independently testable and usable wherever practical.

### Composition over Inheritance

Prefer small, explicit services and composition over expanding inheritance hierarchies. Use inheritance only when a stable shared contract and genuine substitutability exist.

### Predictability

Design for tail latency, overload, backpressure, cancellation, error propagation, and graceful shutdown in addition to average performance. Prefer behavior that can be understood and verified over clever but opaque implementations.

### Measurement-Driven Optimization

Validate performance claims with profiling or reproducible benchmarks. Do not introduce pooling, zero-copy techniques, lock-free structures, or native code based on speculation alone.

### Observability

Consider structured logging, metrics, tracing, and health state in critical execution paths. Do not treat operational diagnostics as a feature to add after implementation.

### Secure Defaults

Consider input validation, authentication and authorization boundaries, secret management, rate limiting, and replay or forgery prevention from the beginning of the design.

## 3. Technology and API Principles

- Center public APIs and default implementations on C# and .NET.
- Integrate naturally with the standard .NET Generic Host, dependency injection, configuration, logging, and `CancellationToken`.
- Prefer async I/O and propagate cancellation tokens. Do not wrap compute-bound work in asynchronous APIs without a concrete reason.
- Design APIs together with their use cases, error models, lifetimes, cancellation behavior, and lifecycle semantics.
- Do not expose internal infrastructure, databases, native handles, or P/Invoke details through public APIs.
- Measure allocation and garbage-collection effects on high-frequency paths. Make ownership and lifetime explicit for buffers, messages, and entity state.
- Prevent unbounded queue growth by defining capacity and overload policy. State whether overload causes rejection, delay, or dropping.
- Treat public APIs, wire protocols, persisted data formats, and native ABIs as separate compatibility boundaries.

## 4. Native Code Boundary

Consider native or C++ implementations only after a managed implementation has established correct behavior and APIs, and measurement has identified a relevant bottleneck.

- Isolate native code in an internal layer.
- Place it behind a small, stable ABI or managed facade.
- Define resource lifetime, error translation, ABI versioning, and platform-specific deployment behavior.
- Retain a managed fallback where practical.
- Do not design APIs that require ordinary users to understand the existence of the native implementation.

## 5. Build System Rules

### Native Build Rules

- Perform native C++ builds through `Aether.BuildTool`.
- Do not use generated Visual Studio C++ projects as the source of truth for build definitions, and do not edit them directly.
- Declare native modules, source directories, include paths, and dependencies in `*.Module.json`.
- Use the Build Tool definitions `PLATFORM_WINDOWS`, `PLATFORM_LINUX`, and `PLATFORM_MACOS` instead of compiler-specific macros for platform branches.
- Regenerate Visual Studio projects with `GenerateSolution.bat` or the Build Tool `generate` command when required.
- Build Windows, Linux, and macOS binaries on their respective local operating systems or CI runners unless an explicitly supported cross-compilation path exists.
- Follow `docs/BUILDING.md` for detailed procedures and commands.

## 6. Work Process

Follow this sequence for implementation and design work:

1. Define the problem, user scenario, target server role, and operating context.
2. Identify relevant quality attributes and constraints, such as latency, throughput, consistency, recovery, and deployment.
3. Propose the smallest realistic usage example and public API first.
4. Review module responsibilities, dependency direction, failure behavior, and lifecycle behavior.
5. Compare alternatives and trade-offs. Leave choices undecided when the available evidence is insufficient.
6. Implement the smallest useful vertical slice. Do not prebuild speculative abstractions or future features.
7. Verify cancellation, error, overload, restart, and shutdown scenarios in addition to the successful path.
8. When making performance claims, record the workload, environment, baseline, and results in a reproducible form.
9. Review compatibility effects separately for public APIs, protocols, persisted data, and native ABIs.
10. Record important decisions in ADRs.

When a request is ambiguous, begin with safe, in-scope reading, analysis, and verification. Confirm the necessary choice before changing the user's intent or making meaningful changes to external systems.

## 7. Testing and Verification

- Consider usage examples and unit tests alongside every public API.
- Make external effects such as time, transport, storage, and scheduling controllable in tests.
- Do not assume unit tests are sufficient. Add integration, load, soak, or fault-injection tests when the risk warrants them.
- Tests must verify behavioral contracts and failure semantics without excessive coupling to implementation details.
- After a change, run the relevant builds, tests, benchmarks, and documentation checks in proportion to its impact.
- Clearly report anything that could not be verified and any environmental limitations.

## 8. Documentation and Decisions

- Write every `AGENTS.md`, `SKILL.md`, and human-maintained document under `docs/` in English. This rule applies to new files and to all additions or revisions to existing files.
- Do not interpret examples or candidate lists in documentation as finalized technology choices.
- Document usage examples and design rationale for new public contracts or structural changes.
- Record important decisions in ADRs, including status, context, decision, alternatives, and consequences.
- Design documents must explain why a choice was made and under what conditions it should be reconsidered, not only what was selected.
- When code and documentation disagree, update both within the task scope or state why the inconsistency could not be resolved.

## 9. Naming and Compatibility

- Use `Aether` for the product and framework name, and `AetherFabric` for the ecosystem and organization brand.
- Use the `Aether.{Identifier}` form by default for .NET namespaces, projects, assemblies, and NuGet packages.
- Keep module identifiers concise and descriptive of their responsibility.
- Do not create a new package when an existing module can naturally own the capability.
- Evaluate compatibility changes separately for public APIs, wire protocols, persisted formats, and native ABIs.

## 10. Git Workflow

- Preserve existing user changes. Do not overwrite or revert unrelated work.
- Check repository status and the current branch before starting work.
- Use a dedicated `codex/` task branch when working in a linked Git worktree. In a primary working tree, remain on the current branch unless the user requests otherwise.
- Commit each coherent feature-sized unit after implementation and verification are complete.
- Do not run destructive commands without an explicit request and an exact, verified target.
- Write concise commit messages that communicate the intent and essential change.

## 11. Result Reporting

At the end of a task, report the following concisely:

- What changed
- Which verification steps ran and their results
- Any remaining limitations or decisions that require attention
- Relevant files, ADRs, or documentation

Each task request takes precedence over this document when defining concrete project goals and priorities. If a request conflicts with these general guidelines, explain the conflict and obtain explicit direction from the user.
