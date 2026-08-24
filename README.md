# Librariann Plex Patcher

A small Windows desktop app that adds a **Librariann** entry to Plex Web's own left sidebar, by
directly patching the `index.html` Plex Media Server serves at `/web`.

It is a standalone tool, separate from Librariann itself — Librariann only needs to expose a URL
(and, optionally, an allowlisted embedding origin) for this app to point at.

## Why a patcher, not a proxy

An earlier prototype tried a network-level bridge: a small proxy in front of Plex's port that
passed traffic through untouched and only intercepted the plain-HTTP `/web` request to inject a
script. That approach was rejected — it can't coexist with a server that already has its own
reverse proxy or tunnel in front of Plex.

Plex Web is not a plugin surface; there's no extension API to hook into. But it *is* just a static
HTML/JS bundle Plex serves from its own install directory, which means the only thing needed to
run custom JS inside it is to add one `<script>` tag to that `index.html` before `</body>`. That's
what this app does — no proxy, no traffic interception, no change to how Plex is reached over the
network.

**Trade-off, accepted by design:** Plex overwrites its bundled web files on every Plex Media
Server update, silently removing the patch. The app doesn't try to survive updates automatically —
it detects the patch is missing and you click **Patch** again.

## What the injected script does

Once it's running inside Plex Web's own page (see
[`Assets/patch-script-template.txt`](src/LibrariannPlexPatcher/Assets/patch-script-template.txt)):

- Waits for Plex's sidebar to exist (it renders client-side) and clones an existing sidebar item —
  by visible text, not Plex's hashed CSS-module class names, so it inherits real styling for free.
- Adds a **Librariann** entry with the Librariann logo, docked into Plex's own flex layout as a
  real sibling of the content pane (not a floating overlay), so it reflows correctly whenever the
  sidebar collapses/expands.
- Clicking it hides Plex's content pane and shows an `<iframe>` pointed at Librariann's chrome-less
  `/embed?nav=1` route, in place — closing again on any real Plex navigation (Plex Web is a
  hash-router, so this is a `hashchange` listener).
- A `MutationObserver` re-injects the item any time Plex redraws the sidebar (e.g. switching in
  and out of the "More"/"Pinned" flyout tears down and rebuilds the container).

The `__LIBRARIANN_URL__` placeholder in the template is filled in with the address you configure
in the app before the block is written.

## How the patch itself works

See [`Services/PatchScriptBuilder.cs`](src/LibrariannPlexPatcher/Services/PatchScriptBuilder.cs)
and [`Services/PlexIndexLocator.cs`](src/LibrariannPlexPatcher/Services/PlexIndexLocator.cs).

1. **Locate** — searches the configured Plex install folder for `index.html`, preferring one under
   a `WebClient.bundle` folder, and confirms a candidate is really Plex's web client (not some
   unrelated file) by checking for a fingerprint string (`downloadFileFrame`) that's always present
   in Plex's real bundle. The exact path isn't hardcoded because it differs by OS and Plex version.
2. **Back up** — the first time a file without an existing patch marker is touched, it's copied to
   `%AppData%\LibrariannPlexPatcher\backups\index.html.<timestamp>.bak` before any write. That
   backup is never overwritten by a later patch, so it always represents Plex's true original.
3. **Inject idempotently** — the block is wrapped in `<!-- librariann-patch:begin -->` /
   `<!-- librariann-patch:end -->` markers. Re-patching (e.g. after changing the Librariann
   address) replaces the block in place instead of duplicating it. The block is inserted right
   before Plex's own unclosed `<iframe name="downloadFileFrame">` tag — HTML5 parsing rules treat
   everything *after* an unclosed `<iframe>` as inert text, so content placed after it would never
   actually execute. If that anchor isn't found (a Plex update changed the markup), it falls back
   to inserting before `</body>`.
4. **Deploy static assets** — the two logo PNGs the injected script references are copied
   alongside `index.html` into a `static` subfolder so Plex serves them at `/web/static/...`.
5. **Restore** — copies the backup back over the live `index.html`.

Only the actual patch/restore file write needs administrator rights (Plex's install folder usually
lives under `Program Files`). The app runs as a normal, unelevated window for everything else
(browsing for the folder, testing the Librariann connection); clicking **Patch** or **Restore**
relaunches the same executable with `--elevated-action <args-file>`, elevated via the UAC "runas"
verb. Arguments and the result cross that process boundary through temporary JSON files (`runas`
via `UseShellExecute` rules out redirecting stdin/stdout directly). See
[`Services/ElevatedActionRunner.cs`](src/LibrariannPlexPatcher/Services/ElevatedActionRunner.cs) /
[`Services/ElevatedActionExecutor.cs`](src/LibrariannPlexPatcher/Services/ElevatedActionExecutor.cs).

## Using the app

1. **Plex install folder** — auto-fills `C:\Program Files\Plex\Plex Media Server` if it exists;
   otherwise browse to it. This is the *install* folder, not the library/metadata folder.
2. **Librariann address** — a bare host, `host:port`, or full URL. Bare addresses are normalized:
   `localhost`/IP literals default to `http://`, anything else defaults to `https://` (matching the
   common case of a LAN Librariann vs. one reached through a TLS-terminating tunnel).
3. **Test Connection** — `GET`s `<address>/embed` and reports success/failure.
4. **Patch** — locates `index.html`, prompts for admin permission once, backs up (first time only),
   injects/updates the script block, and deploys the logo assets. Settings (Plex folder, Librariann
   address, resolved `index.html` path, backup path, last backup/patch timestamps) are saved to
   `%AppData%\LibrariannPlexPatcher\settings.json`.
5. **Restore** — writes the original backup back over the patched file. Enabled only once a backup
   exists on disk.

After any Plex Media Server update, the sidebar entry will disappear — open the app and click
**Patch** again.

## Librariann-side requirement: `EmbeddingOrigins`

The in-page panel loads Librariann's `/embed` route in an `<iframe>`. By default Librariann sends
`frame-ancestors 'self'` / `X-Frame-Options: SAMEORIGIN` and refuses to be framed by anything,
Plex included. For the panel to render instead of coming up blank, an admin has to add **the exact
origin the browser uses to reach Plex** (scheme + host + port — no path, no wildcard) to
Librariann's own `config/appsettings.json`, then restart Librariann:

```json
{
  "EmbeddingOrigins": [
    "http://192.168.1.50:32400"
  ]
}
```

This is Plex's origin as the *browser* sees it, not Librariann's own address — `localhost`,
`127.0.0.1`, a LAN IP, and `https://app.plex.tv` (Plex's hosted web client, for remote/relay
access) are all different origins and each needs its own entry if used.

Expect a real login inside the panel the first time in each browser even if you're already logged
into Librariann in another tab — browsers partition storage for a cross-site iframe like this one
separately from a normal same-site tab. This is inherent to how browsers isolate cross-site iframes
and isn't something either app can configure away.

## Building

Requires the .NET 8 SDK and Windows (WPF, win-x64 only — this only ever patches a Windows Plex
install).

```bash
dotnet build LibrariannPlexPatcher.slnx
```

To produce the distributable self-contained single-file `.exe` (bundles the .NET runtime, so end
users don't need .NET installed):

```bash
dotnet publish src/LibrariannPlexPatcher -c Release
```

## Project layout

```
src/LibrariannPlexPatcher/
├── App.xaml(.cs)                    Startup; also the entry point for the hidden elevated relaunch
├── MainWindow.xaml(.cs)             The one visible window (folder/address inputs, Patch/Restore/Test)
├── Models/
│   ├── PatcherSettings.cs           Persisted to %AppData%\LibrariannPlexPatcher\settings.json
│   └── ElevatedAction.cs            Args/result DTOs passed to the elevated relaunch via temp files
├── Services/
│   ├── PlexIndexLocator.cs          Finds Plex's bundled index.html under the install root
│   ├── PatchScriptBuilder.cs        Builds/injects/detects the marked script block
│   ├── ElevatedActionRunner.cs      Relaunches self with UAC "runas" for the actual file write
│   ├── ElevatedActionExecutor.cs    Runs inside that elevated relaunch; does the patch/restore
│   ├── ConnectionTester.cs          Normalizes + tests the configured Librariann address
│   └── SettingsStore.cs             Loads/saves settings.json
└── Assets/
    ├── patch-script-template.txt    The injected script (embedded resource)
    └── static/logo-*.png            Sidebar icons, deployed into Plex's web folder on patch
```

## Known limitations

- Windows only.
- If Plex changes its sidebar DOM structure enough that the "Home"/"Discover"/"Watchlist" text
  match fails, the injected script simply won't find anywhere to attach the sidebar item (no
  fixed-position fallback is currently implemented).
- No unpatch/verify beyond what **Restore** and **Patch**'s idempotent marker check give you — there's
  no separate "detect drift against the Plex version last patched" check yet.
