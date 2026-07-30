# Eagle Enterprise Edition

Eagle Enterprise Edition (EEE) is the enterprise plugin suite for
[Eagle](https://urn.to/r/code), the Tcl-compatible scripting language for the
Common Language Runtime (CLR).  It extends the Eagle runtime with
production-grade security, cryptography, and tooling -- most notably the ability
to cryptographically sign, license, and verify scripts -- so that trusted,
tamper-evident code can be distributed and executed with confidence.

[![License: Permissive](https://img.shields.io/badge/License-Permissive-blue.svg)](license.terms)
[![Discord](https://img.shields.io/discord/1147557513588375572?style=flat-square&label=Discord&logo=discord&logoColor=white&color=7289DA)](https://urn.to/r/discord)

## Overview

EEE is delivered as a set of optional plugins that load into any Eagle
interpreter.  They build against the Eagle core runtime (see "Building EEE"
below) and run anywhere Eagle does -- .NET Framework 2.0 through .NET 10,
.NET Standard 2.0/2.1, .NET Core, and Mono -- with the user-interface and
native plugins (HotKey, Featherlight) being Windows-specific.

| Plugin | Purpose |
|---|---|
| **Harpy** | The flagship security plugin: a complete script signing and licensing system -- certificates, key pairs, key rings, and secrets -- used to cryptographically sign Eagle scripts and verify them before execution. |
| **Badge** | Certificate and embedded-secret management; the companion to Harpy.  Together they form the Eagle security toolset. |
| **Zeus** | Cryptographic utilities, including password-based (PBKDF2) encryption and decryption, plus supplemental math functions. |
| **Kapok** | Data and configuration management with certificate-based licensing. |
| **HotKey** | System-wide (global) hotkey registration and dispatch, on a dedicated user-interface thread *(Windows)*. |
| **Featherlight** | A rich, interactive WPF host environment for Eagle *(Windows)*. |
| **Demo** | A worked example (assembly *Spizaetus*) that demonstrates replacing the interpreter host at load time. |
| **Aquila** | A ready-to-fork skeleton plugin -- the recommended starting point for building your own enterprise plugins. |

Each plugin is a standard Eagle `[load]`-able assembly; once loaded, its
commands are available to scripts like any other.  See the
[documentation](https://urn.to/r/docs) for the per-plugin command reference.

## Repository Structure

EEE is a **submodule** of the Eagle core super-repository.  Everything under
`Eagle/` is an *overlay*: the meld tool (`Tools/link.eagle`, an Eagle script)
links it into the matching paths of the parent core checkout, so the enterprise
solutions, plugin projects, signing keys, and shared sources appear where the
core's own project files expect them.

```
.
├── Eagle/                                Enterprise overlay (melded into the core)
│   ├── EagleEnterprise*.sln              Enterprise solutions (VS 2005-2022 + NetStandard2X)
│   ├── EagleExtra*.sln                   Supplementary "extra" solutions
│   ├── Plugins/Commercial/Enterprise/    The EEE plugins (the heart of this repo)
│   │   ├── Harpy/ Badge/ Zeus/ Kapok/ HotKey/ Featherlight/ Demo/ Aquila/
│   │   └── certificate.exml, keyRing.*, license.terms
│   ├── Keys/                             Enterprise/demo public signing keys (*.snk)
│   └── Library/Components/Shared/        Shared enterprise sources (e.g. BinaryLicense.cs)
├── Tools/                                The meld tool: link.eagle (signed; run by Eagle)
├── license.terms                         License (permissive; see below)
├── CONTRIBUTING.md  CLA.md               How to contribute (+ contributor agreement)
├── CODE_OF_CONDUCT.md  SECURITY.md       Community and security policies
└── .github/                              Repository automation
```

## Building EEE

EEE builds as part of the Eagle core super-repository.  Because the meld tool is
itself an Eagle script (`Tools/link.eagle`), the **core is built first**, so the
Eagle shell exists to run it:

1. **Clone the core with submodules** (this brings EEE in under `eee/`):

   ```sh
   git clone --recurse-submodules https://github.com/mistachkin/eagle
   ```

   In an existing clone, instead run:
   `git submodule update --init --recursive`

2. **Build the Eagle core** from the checkout root, producing a runnable
   `EagleShell`:

   - Visual Studio: open `Eagle.sln` (version-specific variants `Eagle2005.sln`
     through `Eagle2022.sln` are provided), or
   - .NET SDK: `dotnet build EagleNetStandard2X.sln`

3. **Meld the overlay** by running `link.eagle` with the `EagleShell` built in
   step 2, from the core checkout root:

   ```sh
   EagleShell eee/Tools/link.eagle
   ```

   This creates the links (directory junctions on Windows) that place the
   enterprise solutions, plugin projects, keys, and shared sources at the
   core-relative paths their project files expect.  It is idempotent and safe to
   re-run; pass `--whatIf` to preview and `--unlink` to remove the links.

4. **Build the Enterprise Edition** from the core checkout root (not from
   `eee/`):

   - Visual Studio: open `EagleEnterprise.sln` (version-specific variants
     `EagleEnterprise2005.sln` through `EagleEnterprise2022.sln` are provided), or
   - .NET SDK: `dotnet build EagleEnterpriseNetStandard2X.sln`

   Each plugin builds to a single loadable assembly.

> Creating file symbolic links on Windows requires elevated administrator
> privileges or Developer Mode; the meld tool fails fast if it cannot.

## Official Link Redirector

The short links below use the **https://urn.to/** redirector, which is
secure-by-design and is owned and operated by
[Mistachkin Systems](https://mistachkin.com/).  For additional security via a
DNSSEC-secured domain, **https://w.sb/** may be used in place of
**https://urn.to/** (e.g., `https://w.sb/r/code` instead of
`https://urn.to/r/code`).  Both domains support the same set of links.

## Related Eagle Projects

| Repository | Link | Description |
|---|---|---|
| Core Language | https://urn.to/r/code | The primary Eagle source tree containing the interpreter, core class libraries, and test suite. |
| Documentation | https://urn.to/r/docs | User-facing documentation including command references, API guides, and integration tutorials. |
| Enterprise Edition | https://urn.to/r/eee | The Eagle Enterprise Edition source tree containing advanced plugins, tooling, and test suites. |
| Package Client Toolset | https://urn.to/r/pkgt | Client-side tooling for discovering, downloading, and managing Eagle script packages. |
| Log Monitor | https://urn.to/r/watchCat | A real-time log monitoring and alerting utility built on the Eagle runtime. |
| Extra Tools | https://urn.to/r/extra | Supplementary tools and utilities that extend Eagle, including diagnostics, build helpers, and code-generation aids. |
| Live Demo | https://urn.to/r/demo | An interactive, browser-based environment for experimenting with Eagle scripts without any local installation. |
| Discord Server | https://urn.to/r/discord | Community chat server for discussion, support, and collaboration around Eagle development. |

## License

EEE is released under a permissive, BSD/Tcl-style license -- free to use, copy,
modify, distribute, and license for any purpose, with no royalty or written
agreement required.  See [`license.terms`](license.terms) for the full text,
including the "Commercial Use Transparency" provision and the standard
disclaimer of warranties.

## Contributing

Contributions are welcome.  Please read [`CONTRIBUTING.md`](CONTRIBUTING.md) and
the contributor license agreement in [`CLA.md`](CLA.md) before opening a pull
request, and review the [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md).  To report a
security issue, please follow [`SECURITY.md`](SECURITY.md).
