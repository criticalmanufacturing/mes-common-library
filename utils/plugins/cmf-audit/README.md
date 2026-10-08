# cmf-audit

Plugin used to audit CM CLI based projects. This repo is split in two parts:

- **Root** (`package.json`) — the build/release orchestrator. Not published; it drives `dotnet publish` for every platform and wraps the `npm/` package's version bump/publish scripts.
- **`npm/`** (`npm/package.json`) — the actual package published as `@criticalmanufacturing/audit`. `postinstall.js` copies the right platform binary from `dist/<rid>` into `node_modules/.bin/...` when a consumer installs it. Its `files` field (`dist`, `postinstall.js`, `run.js`, `utils.js`) is exactly what ends up in the published tarball.

## Prerequisites

- **.NET SDK 8.0.x**.
- **Node.js** (>= 16.7) and **npm**.
- `CriticalManufacturing.CLI.Core` is restored from nuget.org (see the repository's `NuGet.Config`).

## Telemetry and data handling

- **Telemetry is off by default.** Set `cmf_audit_enable_telemetry=1` (and optionally `cmf_audit_enable_extended_telemetry=1`) to opt in.
- **`--postEvent`** sends audit results to the MES system given by `--hostAddress`. For Data packages this includes the full content of master data records. Only use it against systems you are allowed to send that data to, and keep `--useSSL` enabled.
- **Tokens:** prefer environment variables over command-line arguments, which show up in process lists and shell history. Use `SYSTEM_ACCESSTOKEN` for the Azure DevOps PAT and `CMF_AUDIT_SECURITY_TOKEN` for the MES security token.
- **Local debugging:** copy `src/Properties/launchSettings.example.json` to `launchSettings.json`, which is git-ignored, and never commit real tokens.

## 1. Install dependencies

From `utils/plugins/cmf-audit/`:

```powershell
npm install
```

This installs the build tooling (`rimraf`, `dotnet-bump`, `standard-version`) used by the `build:*`/`bump:*` scripts.

Then install the publishable package's own dependencies (`debug`, `mkdirp`, `node_modules-path`, `rimraf` — these are what `postinstall.js`/`run.js` need at runtime on a consumer's machine):

```powershell
cd npm
npm install
cd ..
```

This also (re)generates `npm/package-lock.json`, which must be committed alongside `npm/package.json`.

To run the unit tests: `dotnet test tests`.

> `dotnet restore` does not need to be run separately — `dotnet publish` (used below) restores automatically.

## 2. Build the binaries

Clean any previous build output, then cross-compile for all three platforms into `npm/dist/`, which is what actually gets bundled into the tarball (the root's `dist/` from `build:prod` is not part of the npm package):

```powershell
npm run build:clean
npm run build:bundle
```

This runs `dotnet publish` for `win-x64`, `linux-x64` and `osx-x64` into `npm/dist/<rid>/`. To build a single platform instead, use one of:

```powershell
npm run build:bundle:win
npm run build:bundle:linux
npm run build:bundle:osx
```

## 3. Generate the tarball

With `npm/dist/<rid>/` populated, pack the `npm/` folder — this respects the `files` field in `npm/package.json`, so it only includes `dist`, `postinstall.js`, `run.js` and `utils.js`:

```powershell
cd npm
npm pack
cd ..
```

This produces `criticalmanufacturing-audit-<version>.tgz` in `npm/`. Sanity-check its contents before publishing:

```powershell
tar -tzf npm/criticalmanufacturing-audit-*.tgz
```

You should see `package/dist/win-x64/...`, `package/dist/linux-x64/...`, `package/dist/osx-x64/...`, plus `package/postinstall.js`, `package/run.js`, `package/utils.js` and `package/package.json`. If `dist/` is missing or only has one platform, re-run step 2 — a partial `build:bundle` (e.g. only `build:bundle:win`) will produce a tarball that only works on that platform.

## 4. Publishing (optional)

If you also want to publish instead of just producing the tarball locally, bump the version first (`npm run bump:pre` / `bump:patch` / `bump:feature` / `bump:breaking`, which bumps both `npm/package.json` and the `.csproj` version in lockstep), rebuild with `build:bundle`, then:

```powershell
npm run publish       # publishes with the "next" tag
npm run publish:live  # publishes with the "latest" tag
```

Both `cd` into `npm/` and run `npm publish` there, so the same `files` filtering applies as in step 3.
