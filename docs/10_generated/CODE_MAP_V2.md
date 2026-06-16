# SCADA Builder V2 - Code Map

Date: 2026-06-16
Status: Generated baseline; verification required
Document version: `V2.1.1.0039`

## Historique des changements

| Date | Version | Commit | Changement |
| --- | --- | --- | --- |
| 2026-06-16 | `V2.1.1.0039` | `PENDING` | Creation de la carte code initiale pour verification documentaire. |

## 1. Modules

| Module | Responsibility |
| --- | --- |
| `src/ScadaBuilderV2.Domain` | Durable project, scene, element, versioning, and pure domain rules |
| `src/ScadaBuilderV2.Application` | Commands, history, conversion, selection models, Studio factories |
| `src/ScadaBuilderV2.Infrastructure` | Persistence, legacy readers, package IO, adapters |
| `src/ScadaBuilderV2.Rendering` | Preview/export rendering and FT100 package generation |
| `src/ScadaBuilderV2.App` | WPF editor shell and WebView bridge |
| `src/ScadaBuilderV2.ElementStudio.App` | Studio Element+ WPF application |
| `tests/ScadaBuilderV2.Tests` | Regression coverage |

## 2. Verification

Run:

```powershell
powershell -ExecutionPolicy Bypass -File tools/docs/verify-docs.ps1
```
