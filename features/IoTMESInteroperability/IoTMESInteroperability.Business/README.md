# IoT MES Interoperability Business

`Cmf.Community.IoTMESInteroperability.Business` is the Business package for the IoT MES Interoperability feature. It contains the compiled .NET assemblies that implement the orchestration and service APIs consumed by MES and Connect IoT customizations.

## Components

| Component | Location | Purpose |
| --- | --- | --- |
| Orchestration | [`Cmf.Community.IoTMESInteroperability.Orchestration/`](Cmf.Community.IoTMESInteroperability.Orchestration/) | Exposes `ICommunityOrchestration` operations and their input/output objects. |
| Services | [`Cmf.Community.IoTMESInteroperability.Services/`](Cmf.Community.IoTMESInteroperability.Services/) | Provides the `CommunityController` service endpoint. |
| Common | [`Cmf.Community.IoTMESInteroperability.Common/`](Cmf.Community.IoTMESInteroperability.Common/) | Shared Business-layer code and project dependencies. |
| Tests | [`Tests/`](Tests/) | Orchestration unit tests for the interoperability operations. |

The public orchestration contract currently provides:

- `IoTMaterialOperation`, for material operation execution from IoT.
- `GetMaterialRecipe`, for retrieving a material recipe for IoT processing.

The Business solution is built from [`Business.sln`](Business.sln), with compiled assemblies emitted to [`Release/`](Release/).

## `cmfpackage.json`

This is an installable, non-unique `Business` package. Its manifest packs all DLLs matching `Release/*.dll` into the package root. The root feature package installs this package alongside the Data package, while the Data package also declares this Business package as a pre-build related package because its DEE code references these assemblies.

## Build and deployment

Build [`Business.sln`](Business.sln) with the feature's configured MES/.NET toolchain. The package manifest expects the resulting assemblies in `Release/`; deploy this package as part of the root `Cmf.Community.IoTMESInteroperability` package.