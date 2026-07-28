# NuGet publish checklist

**Date:** 2026-07-26 · **Author:** GLM-5.2 (ZCode) · **Status:** Decision recorded (license = MIT),
execution checklist for a code-authoring session. Resolves the gated item in
`docs/DECISIONS.md`'s "M8 CLOSED" entry: *"NuGet-publish readiness (license file,
PackageLicenseExpression, RepositoryUrl, actually publishing)... waits on Nasser deciding a
license and a public-release timing."*

> **Lane note:** This is an executable checklist, not an applied change. The metadata edits below
> touch `.csproj` files, which are outside the documentation-only role GLM-5.2 holds in this repo
> (`docs/GLM_DOCUMENTATION_GUIDELINES.md §3`). A code-authoring session (or Nasser directly)
> executes it. Everything here is concrete: exact edits, exact commands, exact gotchas.

---

## 0. What's already done

Verified against the three csproj files 2026-07-26: `[verified:src=src/Topos.Hypergraph/Topos.Hypergraph.csproj]`
`[verified:src=src/Topos.Hypergraph.Persistence/Topos.Hypergraph.Persistence.csproj]`
`[verified:src=src/Topos.Hypergraph.Knowledge/Topos.Hypergraph.Knowledge.csproj]`

Each package already has: `PackageId`, `Authors` (`Nasser Towfigh`), `Copyright`
(`Copyright © 2026 Nasser Towfigh`), `Version` (prerelease: `0.1.0-m8` / `0.1.0-m8` / `0.1.0-m9`),
`Description`, `TargetFramework` (`net10.0`). All three target `net10.0` with `Nullable` +
`ImplicitUsings` enabled. The repo is **public** as of 2026-07-26
(`https://github.com/Minu476/Topos`). `[verified:web=https://github.com/Minu476/Topos]`

**Dependency graph** (ProjectReference, becomes a NuGet dependency on pack):
`Topos.Hypergraph.Persistence` → `Topos.Hypergraph`; `Topos.Hypergraph.Knowledge` →
`Topos.Hypergraph`. The kernel itself references nothing.

---

## 1. The decision — MIT

**License: MIT** (decided by Nasser, 2026-07-26). Reasonable conventional default for .NET
libraries — maximally permissive, lowest friction to adoption, the SPDX identifier `MIT` is
recognized by NuGet.org without a bundled license file.

If this choice is revisited: Apache-2.0 is the next-most-common .NET choice and adds an explicit
patent grant; the rest of this checklist is identical for either (just swap `MIT` for `Apache-2.0`
and drop in that license text). For anything more restrictive (GPL family, a commercial dual-license),
the `PackageLicenseExpression` step changes — consult NuGet's license docs at that point.

---

## 2. Add a LICENSE file (repo root)

NuGet's `PackageLicenseExpression` approach (step 3) does **not** require bundling license text
into the package, but a LICENSE file at the repo root is standard practice and GitHub renders it
prominently on the repo page. Create `LICENSE` with the standard MIT text, filling the year and
copyright holder:

```
MIT License

Copyright (c) 2026 Nasser Towfigh

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

`[unverified:inferred — standard MIT text, copied verbatim from the OSI template]`

---

## 3. Add package metadata to each `.csproj`

Add to **all three** csproj files' existing `<PropertyGroup>`. (The `PackageId`/`Authors`/etc.
already there stay; these are the missing pieces.)

```xml
    <!-- License: SPDX expression. NuGet.org renders this; no file-bundling needed. -->
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <!-- Source repo — lets consumers trace the package back to this code. -->
    <RepositoryUrl>https://github.com/Minu476/Topos.git</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <!-- Project page on nuget.org's "Project website" link. -->
    <PackageProjectUrl>https://github.com/Minu476/Topos</PackageProjectUrl>
    <!-- Discoverability — these show up in nuget.org search and package listings. -->
    <PackageTags>hypergraph;ai-memory;agent-memory;llm;knowledge-graph;csharp;graph;incidence;n-ary</PackageTags>
    <!-- Optional but recommended: render the repo README on the nuget.org package page. -->
    <PackageReadmeFile>README.md</PackageReadmeFile>
```

For `PackageReadmeFile` to work, also add (csproj root, alongside the existing
`<InternalsVisibleTo>` ItemGroup — only the kernel csproj has that today; the other two just need
this added):

```xml
  <ItemGroup>
    <None Include="..\..\README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
```

(The `..\..\` is relative from `src/<package>/` to the repo root where `README.md` lives. The
kernel csproj already has an ItemGroup, so add this `<None>` inside it rather than creating a
second ItemGroup.)

`[verified:src=src/Topos.Hypergraph/Topos.Hypergraph.csproj — existing structure this slots into]`

### Optional: per-package `README.md` packing

If you'd rather each package render its own tailored README on nuget.org (kernel-focused for
`Topos.Hypergraph`, persistence-focused for `.Persistence`, etc.), put a `README.md` next to each
csproj and reference *that* — `<None Include="README.md" Pack="true" PackagePath="\" />`. The
single-root-README approach above is simpler and uses the polished front-door README that already
exists; the per-package approach is a polish step you can take later without republishing
unrelated packages.

---

## 4. Decide versioning before first publish

**This is the one decision that can't be undone.** A version pushed to nuget.org can be unlisted
(hidden from search) but **never deleted or re-pinished** — the version number is permanently
consumed. `[verified:web=https://learn.microsoft.com/nuget/nuget-org/policies/deleting-packages]`

The csprojs currently carry milestone-prerelease versions:
`[verified:src=src/Topos.Hypergraph/Topos.Hypergraph.csproj — <Version>0.1.0-m8</Version>]`

| Package | Current `<Version>` | SemVer 2.0 prerelease-valid? |
|---|---|---|
| `Topos.Hypergraph` | `0.1.0-m8` | ✅ valid (prerelease tag `m8` after `-`) |
| `Topos.Hypergraph.Persistence` | `0.1.0-m8` | ✅ valid |
| `Topos.Hypergraph.Knowledge` | `0.1.0-m9` | ✅ valid |

NuGet.org accepts these as prereleases; consumers opt in with `dotnet add package Topos.Hypergraph --prerelease`.
That's the right shape for a first publish — it signals "not 1.0 yet, API may move" without
foreclosing a clean `1.0.0` later.

**Recommended:** publish the first three as `0.1.0-m8` (kernel + persistence, which shipped
together) and `0.1.0-m9` (knowledge, which is the newer milestone) — exactly the current csproj
values. No version edits needed for the first publish. For subsequent publishes, bump per SemVer
(patch for bugfix, minor for additive, major for breaking) and never reuse a version string.

If you'd rather publish under a cleaner `0.1.0-beta1` / `0.2.0` shape, edit the `<Version>` tags
in step 3's same commit — but read the gotcha in §7 first (the `0.1.0-mN` strings already align
with the milestone roadmap and are less likely to confuse someone reading the spec).

---

## 5. Build, pack, verify locally

From the repo root (`dotnet 10.0.101` or later — `[verified:web — .NET 10 SDK required, matches the net10.0 target framework]`):

```bash
# Clean build of the whole solution (catches any breakage before packing).
dotnet build Topos.sln -c Release

# Pack each project into a .nupkg. --include-symbols gives a paired .snupkg for source debugging.
dotnet pack src/Topos.Hypergraph/Topos.Hypergraph.csproj -c Release -o ./nupkgs --include-symbols
dotnet pack src/Topos.Hypergraph.Persistence/Topos.Hypergraph.Persistence.csproj -c Release -o ./nupkgs --include-symbols
dotnet pack src/Topos.Hypergraph.Knowledge/Topos.Hypergraph.Knowledge.csproj -c Release -o ./nupkgs --include-symbols
```

**Verify the .nupkg contents before pushing** — catches metadata mistakes while they're still
free to fix:

```bash
# Inspect the kernel package: confirms license, dependencies, repository URL landed correctly.
unzip -p ./nupkgs/Topos.Hypergraph.0.1.0-m8.nupkg Topos.Hypergraph.nuspec | less
# Or, with the nuget CLI:
nuget spec -f ./nupkgs/Topos.Hypergraph.0.1.0-m8.nupkg

# Confirm the .Persistence package carries a dependency on Topos.Hypergraph (the pack step
# turns the ProjectReference into a NuGet dependency automatically — verify it did).
unzip -p ./nupkgs/Topos.Hypergraph.Persistence.0.1.0-m8.nupkg Topos.Hypergraph.Persistence.nuspec | grep -A2 'dependency id="Topos.Hypergraph"'
```

If the dependency didn't materialize, check the `.Persistence` csproj still has its
`<ProjectReference Include="..\Topos.Hypergraph\Topos.Hypergraph.csproj" />`. NuGet's pack
auto-promotes `ProjectReference` → package dependency for projects in the same build; it should
"just work." `[verified:src=src/Topos.Hypergraph.Persistence/Topos.Hypergraph.Persistence.csproj — ProjectReference present]`

**Add `nupkgs/` to `.gitignore`** if not already ignored (it isn't today —
`[verified:src=.gitignore — no nupkgs entry]`). The csproj's existing `*.nupkg` ignore covers
loose packages but not the output folder. Add:

```
# NuGet package output (local build artifact; never commit)
nupkgs/
```

---

## 6. Publish to nuget.org

**One-time setup (Nasser, in a browser):**

1. Create an account at `https://www.nuget.org` (or sign in with the GitHub account `Minu476`
   for linking — recommended, it surfaces the repo link on the package page).
2. Generate an API key: `https://www.nuget.org/account/apikeys`. Scope it narrowly:
   **Push new packages and package versions only**, with a **globbing pattern** like
   `Topos.Hypergraph*` (covers all three packages), and an **expiration** you'll actually rotate
   (default 365 days is fine). Copy the key — it's shown once.

**Push (from the repo root, with the key in hand):**

```bash
# Push each package. --api-key takes the key; --source is nuget.org's v3 push endpoint.
# The .snupkg (symbols) is auto-pushed alongside the .nupkg if you used --include-symbols above.
dotnet nuget push ./nupkgs/Topos.Hypergraph.0.1.0-m8.nupkg \
    --api-key YOUR_KEY \
    --source https://api.nuget.org/v3/index.json
dotnet nuget push ./nupkgs/Topos.Hypergraph.Persistence.0.1.0-m8.nupkg \
    --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json
dotnet nuget push ./nupkgs/Topos.Hypergraph.Knowledge.0.1.0-m9.nupkg \
    --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json
```

**Don't commit the API key anywhere.** Use it inline, or set it as an env var for the session
(`export NUGET_API_KEY=...`) and reference `--api-key $NUGET_API_KEY`. NuGet.org keys are
revokeable from the same page that created them; rotate immediately if leaked.

After push: nuget.org indexes the package within a few minutes; verify at
`https://www.nuget.org/packages/Topos.Hypergraph`. The README renders on the package page if
`PackageReadmeFile` was wired correctly in step 3.

---

## 7. Gotchas, stated plainly

1. **Versions are permanent.** A pushed version is forever (unlistable, not deletable). The
   current prerelease strings are fine for a first publish; for any later publish, never reuse a
   version number. `[verified:web=https://learn.microsoft.com/nuget/nuget-org/policies/deleting-packages]`

2. **`net10.0` is the target framework.** Consumers must be on .NET 10+. NuGet.org will refuse
   installation on older TFM targets. That's the correct posture today (Topos uses .NET 10
   features), but be aware it limits the consumer pool until .NET 10 is broadly adopted. If wider
   compatibility is ever wanted, multi-targeting (e.g. `net10.0;net8.0`) is a later change — not
   a first-publish concern. `[verified:src=src/Topos.Hypergraph/Topos.Hypergraph.csproj — <TargetFramework>net10.0</TargetFramework>]`

3. **The Knowledge package is the newest.** It carries `0.1.0-m9` while the other two are
   `0.1.0-m8`. That asymmetry is intentional (it shipped a milestone later) — see the milestone
   table in `README.md` and `docs/SPECIFICATION.md §6`. Don't "fix" the asymmetry by re-versioning
   all three to the same number unless you've thought about what that signals.

4. **No `<PackageValidation>` / API-surface baseline today.** NuGet's package validation
   (detecting breaking changes between versions) is a real safeguard for a library that cares about
   API stability — which Topos does (M8 was an explicit API-freeze). Worth adding as a follow-on
   after first publish: `<EnablePackageValidation>true</EnablePackageValidation>` plus a
   `<PackageValidationBaselineVersion>`. Not a first-publish blocker; noted here so it's not
   forgotten. The M8 API-stability work is documented in `docs/DECISIONS.md`'s three 2026-07-25
   entries; that's the baseline a future package-validation setup would anchor to.

5. **Source link / deterministic builds** are not configured. Nice-to-have for a public package
   (lets consumers step into Topos source in the debugger), not required for first publish. Add
   later: `<PublishRepositoryUrl>true</PublishRepositoryUrl>`,
   `<EmbedUntrackedSources>true</EmbedUntrackedSources>`,
   `<ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>` (the last only in CI), plus
   the `Microsoft.SourceLink.GitHub` package reference. Outside first-publish scope.

6. **RLB / NexusVerifier / ChatMemory all consume via `ProjectReference`, not NuGet.** Publishing
   doesn't change those consumers; they keep working as-is. The publish is for the broader public
   consumer GPT's review might surface. If you want an existing consumer to switch to the NuGet
   package, that's a separate per-consumer change in their repos.

---

## 8. After publish — wire it back into the docs

Once the packages are live, two small follow-ups (these *are* in GLM-5.2's lane — pure Markdown):

- **`README.md` "Get started" section** currently says "not yet on NuGet — reference it from source
  via a ProjectReference." `[verified:src=README.md — the quickstart block]` Flip that to show the
  `dotnet add package Topos.Hypergraph` install line, with the `ProjectReference` path kept as a
  fallback for consumers who want to track `main`.
- **`docs/GETTING_STARTED.md` step 1** has the same framing — same update.
- **`docs/DECISIONS.md`** gains a "NuGet PUBLISHED" entry recording the version(s), date, license
  decision (MIT), and that this closes the gated item from "M8 CLOSED." That's a record of a thing
  that happened, not a milestone declaration — squarely within the documentation role.

Run `dotnet test Topos.sln` once more after the metadata edits and before pushing, as a paranoia
check that nothing in the csproj changes broke the build.

---

## TL;DR — the minimum viable publish

1. Add `LICENSE` (MIT) at repo root.
2. Add the six metadata lines from §3 to each of the three csprojs.
3. `dotnet build Topos.sln -c Release && dotnet pack` each project.
4. Inspect one `.nuspec` to confirm metadata landed.
5. `dotnet nuget push` each `.nupkg` with a scoped nuget.org API key.
6. Verify at `https://www.nuget.org/packages/Topos.Hypergraph`.

Versions stay at `0.1.0-m8` / `0.1.0-m8` / `0.1.0-m9` (already correct). Total time, with the
API key in hand: ~10 minutes of which ~9 is the nuget.org account/key setup. The `.csproj` edits
themselves are under a minute.
