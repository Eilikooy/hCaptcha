# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

`Eiliko.Blazor.hCaptcha` is a single-project NuGet library (`Microsoft.NET.Sdk.Razor`, `net10.0`) providing an hCaptcha component for **server-side Blazor**. It is a maintained fork of Texnomic.Blazor.hCaptcha. There are no tests and no sample app in this repo.

## Commands

Run from the repo root (where the `.sln` lives):

```pwsh
dotnet build Eiliko.Blazor.hCaptcha.sln -c Release
dotnet pack Eiliko.Blazor.hCaptcha.sln -c Release --no-build --output ./artifacts
dotnet tool restore; dotnet gitversion        # the version CI would assign to HEAD
dotnet list package --outdated                # what Dependabot will propose
```

Build alone also produces a `.nupkg` (`GeneratePackageOnBuild=true`). `dotnet pack` must run after a build with `--no-build`: from a clean `obj/`, pack on its own fails with a missing `staticwebassets.build.json` manifest, a Razor SDK quirk. That is why CI has separate build and pack steps rather than the `dotnet publish` it used to run.

## CI and versioning

`azure-pipelines.yml` builds on `master`, on `release/*`, and on pull requests, inside the `dotnet/sdk:10.0` container on the self-hosted `Builders` pool: full-depth checkout (GitVersion needs history and tags), `dotnet gitversion /output buildserver`, build, pack, then publish the artifact. Pull requests build but publish nothing; `master` and `release/*` publish.

**Versioning is automatic (GitVersion, TrunkBased workflow, config in `GitVersion.yml`).** Do not edit `<Version>` in the csproj; it holds a `0.0.0-local` placeholder that applies only when CI does not pass one in.

- Every merge or commit on `master` bumps the patch from the latest git tag.
- `+semver: minor` or `+semver: major` in the **merge commit** message bumps that part instead. In a branch commit it also works, but the merge commit still adds a patch on top.
- A pushed tag pins that exact version and becomes the new baseline.
- Versions always increase but can occasionally skip a number if squash merges and merge commits are mixed. Harmless for NuGet; pick one merge strategy to avoid it.

Tags are the baseline and **must be pushed**, or CI computes from the wrong starting point.

### Shipping a release candidate

Master always produces stable version numbers, so an rc comes from a release branch. Branch `release/<version>` off master and push it; the pipeline builds release branches and publishes their artifact.

| Step | Version produced |
|---|---|
| `release/1.0.0` created | `1.0.0-rc.0` |
| each further commit on it | `1.0.0-rc.1`, `1.0.0-rc.2`, ... |
| merged back to master | `1.0.0` stable |

The version comes from the branch name, not from counting commits, so fixes during testing raise the rc number and leave the target version alone. Merging back drops the rc label on its own; tag that commit anyway so it becomes the next baseline.

The first candidate is `rc.0` rather than `rc.1`. That is cosmetic and sorts correctly.

Do not try to produce an rc by tagging master. A tag like `1.0.0-rc.1` there is silently emitted as plain `1.0.0`, because master strips prerelease labels.

## Dependency updates

Dependabot (`.github/dependabot.yml`) opens **one grouped PR weekly** for all NuGet minor and patch updates. It used to open one PR per package daily, which is why nine stale branches accumulated on 9.0.9 while the project moved to 10.0.x through hand-made `pkg_updates_*` branches. Do not go back to per-package PRs.

Major Microsoft package versions are ignored on purpose: they track the .NET major version and need a `TargetFramework` change, so they cannot restore against `net10.0` from a package bump alone. Those upgrades stay manual.

Merging the grouped PR is all that is needed; GitVersion turns it into the next patch version and CI packs it. Dependabot branch names and grouped merges were verified to produce exactly one patch bump.

`.github/workflows/dependabot-auto-merge.yml` can close the last manual step, but it is **inert until the repository variable `DEPENDABOT_AUTOMERGE` is set to `true`**. Before enabling it, branch protection on `master` must require the Azure Pipelines status check, and "Allow auto-merge" must be on for the repository. Without the required check, `gh pr merge --auto` merges immediately and unbuilt code reaches `master`, which then publishes.

## Architecture

The whole library is a JS-interop bridge around hCaptcha's `api.js`, verified server-side:

1. **DI setup** — `Extensions/ServiceCollectionExtensions.AddHCaptcha(...)` binds `HCaptchaConfiguration` (`SiteKey`, `Secret`, optional `ExpectedHostname`, `VerifyUrl`, `ScriptLoadTimeout`) with startup validation, and registers the named `HttpClient` `HCaptchaDefaults.HttpClientName` used for verification. Consumers do not need to call `AddHttpClient()` themselves.
2. **Render** — `HCaptcha.razor` renders only an empty `<div>` with a GUID-based id, splatting any unmatched attributes onto it. `@attributes` is deliberately placed *before* `id` in the markup so the component's own id wins, since later attributes override earlier ones and hCaptcha renders into that id. On first render, `HCaptcha.razor.cs` imports the ES module `wwwroot/scripts/hCaptcha.js` (served at `_content/Eiliko.Blazor.hCaptcha/scripts/hCaptcha.js`) and calls its `render` export. The JS side waits in-browser for the async-loaded global `hcaptcha` object up to `ScriptLoadTimeout`, then returns the widget id, or `null` on timeout (reported to the consumer as error code `hcaptcha-script-not-loaded`). All interop calls carry a `CancellationToken` cancelled in `DisposeAsync`, and `JSDisconnectedException` is swallowed so a dropped circuit never surfaces as an unhandled error.
3. **Callbacks** — JS passes the widget's `callback`, `error-callback` and `expired-callback` back into .NET via a `DotNetObjectReference` and the `[JSInvokable]` methods `HCaptchaOnSuccess`, `HCaptchaOnError`, `HCaptchaOnExpired`. Keep the string names and argument counts in the C# attributes and the JS `invokeMethodAsync` calls in sync; a mismatch throws at dispatch time.
4. **Server-side verification** — `VerifyAsync` POSTs `response`, `secret`, `sitekey` (and `remoteip` if the `RemoteIp` parameter is set) to `VerifyUrl` and deserialises the JSON body. Success requires `success: true` in the body, not just an HTTP 2xx (hCaptcha returns 200 for rejected tokens too), plus a hostname match when `ExpectedHostname` is configured. The outcome is delivered as `HCaptchaVerificationResult` via `OnVerified` and as a `bool` via the legacy `Callback`; every failure path (widget error, expiry, HTTP/JSON error, script not loaded) goes through the same `NotifyAsync` so consumers always hear back.

`Theme` and `Size` enums are lowercased with `ToLowerInvariant()` before being handed to JS, so enum member names must match hCaptcha's option values (`light`/`dark`, `normal`/`compact`). The component exposes `ResetAsync()` because hCaptcha tokens are single-use, and `DisposeAsync` calls `hcaptcha.remove` so widgets do not leak in the DOM.

The README's Setup section is the consumer-facing contract (the `api.js` script tag in `App.razor`, `AddHCaptcha()`, `<HCaptcha Callback=... OnVerified=... Theme=...>`); update it if any of those public surfaces change. README.md and Logo.png are packed into the NuGet package.
