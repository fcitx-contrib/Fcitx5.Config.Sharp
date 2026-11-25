# Fcitx5.Config.Core

A .NET binding for fcitx5's `org.fcitx.Fcitx.Controller1` D-Bus interface, for tools that read and
write the configuration of a running instance.

It is a **configuration** binding, not an input-method runtime: no input contexts, no key events.

## Status

Version 1. Targets `netstandard2.1` and `net8.0`, is Native AOT compatible, and speaks D-Bus on
Linux only ([why](#platform-support)). It is not published to NuGet; reference the project or pack
it locally.

## Requirements

- .NET 8 or later for AOT, or `netstandard2.1` for older runtimes.
- Linux with a D-Bus session bus.
- fcitx5 with its `dbus` addon enabled — that addon registers `org.fcitx.Fcitx5`.

## Getting started

```csharp
using Fcitx5.Config.Core.Fcitx;
using Fcitx5.Config.Core.Model;

await using var controller = await FcitxController.ConnectDbusAsync();

if (!controller.IsAvailable)
{
    Console.WriteLine("fcitx5 is not running.");
    return;
}

Console.WriteLine($"state: {await controller.GetStateAsync()}");

foreach (var addon in await controller.GetAddonsAsync())
{
    Console.WriteLine($"{addon.UniqueName}: enabled={addon.Enabled}, configurable={addon.Configurable}");
}

var global = await controller.GetGlobalConfigAsync();
foreach (var group in global.Description.Types)
{
    Console.WriteLine($"group {group.Name}: {group.Options.Length} options");
}

// The current value, or the declared default when it was never set.
Console.WriteLine(global.GetOptionValue("Behavior/DefaultPageSize")?.Value);
```

Connecting throws only when the bus is unreachable. A missing fcitx5 is not an error: `IsAvailable`
stays false, so a UI may start before the daemon.

## Concepts

### Configuration URIs

`FcitxConfigUri` builds and parses the three shapes:

| URI | Convenience method |
| --- | --- |
| `fcitx://config/global` | `GetGlobalConfigAsync` / `SetGlobalConfigAsync` |
| `fcitx://config/addon/<name>` | `GetAddonConfigAsync(name)` / `SetAddonConfigAsync(name, …)` |
| `fcitx://config/inputmethod/<uniqueName>` | `GetInputMethodConfigAsync(name)` / `SetInputMethodConfigAsync(name, …)` |

`FcitxConfigUri.Addon(name, subPath)` addresses an addon sub-configuration.

### Value and description

`GetConfigAsync` returns both halves the daemon sends:

- `Value` — the configuration as fcitx5 stores it, unfiltered, including keys no widget can show.
- `Description` — the schema: groups of options with type, localized text, default and constraints.

Build a UI from `Description`; read state from `Value`. `FindOption(name)` searches all groups and
`GetOptionValue("Group/Name")` falls back to the declared default.

### RawConfigValue

A node is a string leaf, or a node with an optional own value plus ordered children.

- Lists are children named `"0"`, `"1"`, … in order. Order is preserved because a fcitx5 list is
  order sensitive.
- Booleans are the strings `"True"` and `"False"`; fcitx5 accepts nothing else, so a written
  `"true"` is not a boolean.
- `RawConfigValue.SelfKey` (`""`) is a node's own value on the wire.

`SetPath("A/B/C", value)`, `ToStringList()`, `ToList()` and `FromList()` cover the common shapes.

### Option types and constraints

`OptionDescription.Type` parses the reported type string: `Kind` with the `List|` and `Optional|`
wrappers peeled off (`IsList`, `IsOptional`, `ElementType`), and `GroupName` for nested groups.
Unknown types fall back to `OptionKind.Group`, so a newer daemon does not break an older client.

`Constraints` exposes typed properties — `IntMin`/`IntMax`, `Tooltip`, `ListConstrain`, and
`EnumItems()` pairing each index with its localized label — with `Raw` for anything uncovered.

### Capabilities

`FcitxCapabilities` flags what the transport supports. D-Bus supports all of them; callers should
still check rather than assume, and a second transport then needs no caller changes.

## Errors and availability

| Situation | Behaviour |
| --- | --- |
| No session bus | `DBusConnectFailedException`, or `InvalidOperationException` when no address can be determined |
| fcitx5 not running | Connect succeeds, `IsAvailable` is false |
| Call while unavailable | `DBusErrorReplyException` with `ServiceUnknown`; check `IsAvailable` first |
| fcitx5 starts, exits or restarts | `AvailabilityChanged` fires |
| Bus disconnects | Not re-established; create a new controller |

Daemon refusals carry `ErrorName`, so an unknown addon is distinguishable from a bad argument
without parsing text.

`GetAddonsAsync` falls back to `GetAddons` on daemons predating `GetAddonsV2`; `OnDemand`,
`Dependencies` and `OptionalDependencies` are then empty.

## Writing configuration

Writes are partial: only the keys present in the tree change.

```csharp
var changes = new RawConfigValue();
changes.SetPath("Behavior/DefaultPageSize", "7");
await controller.SetGlobalConfigAsync(changes);
```

For read-modify-write, clone before editing:

```csharp
var snapshot = await controller.GetGlobalConfigAsync();
var edited = snapshot.Value.DeepClone();
edited.SetPath("Behavior/DefaultPageSize", "9");
await controller.SetGlobalConfigAsync(edited);
```

`SetAddonConfigAsync` and `SetInputMethodConfigAsync` work the same way. Two details:

- Addon switches apply at the next fcitx5 start: `SetAddonEnabledAsync` records the override and
  loads nothing.
- Cancellation is checked before dispatch, so a cancelled write never reaches the daemon.

## Native AOT

The D-Bus proxy is generated at compile time from `Transport/Dbus/org.fcitx.Fcitx.Controller1.xml`
by `Tmds.DBus.Generator`, so there is no runtime reflection. The generator is build-time only: the
proxy is compiled into the assembly, and consumers need neither it nor the XML.

`IsAotCompatible` is on for `net8.0` only; the analyzers do not exist for `netstandard2.1`
(`NETSDK1210`).

```bash
scripts/aot-smoke.sh   # publishes a native binary, fails on any trimming or AOT diagnostic
```

It exits 2 when fcitx5 is not running, which the script reports as a skip. The SDK cannot
cross-compile AOT, so publish on the target OS.

## Platform support

Linux is supported.

Windows is out of scope, though the library builds for `win-x64` and D-Bus has an upstream Windows
port:

- No system session bus: an application would ship and supervise `dbus-daemon.exe`.
- Authentication is EXTERNAL over IPv4 loopback, resolved to a process SID, same user only.
- fcitx5's Windows build disables the `dbus` addon.

macOS is deferred. `fcitx5-macos` exposes an HTTP bridge (`fcitx5-beast`) instead of Controller1, so
it needs a second transport here. That is scope, not a blocker: the interface and capability flags
are shaped for it.

## Building and testing

```bash
dotnet build Fcitx5.Config.Sharp.slnx
dotnet test Fcitx5.Config.Core.Tests/Fcitx5.Config.Core.Tests.csproj
```

Bus-dependent tests start a private `dbus-daemon`. Integration tests use the real session bus and
skip when there is no bus, no fcitx5, or not Linux. One test rewrites the user's configuration and is
opt-in:

```bash
FCITX5_INTEGRATION_WRITE=1 dotnet test Fcitx5.Config.Core.Tests/Fcitx5.Config.Core.Tests.csproj
```

## Project layout

```
Fcitx5.Config.Core/
  Model/            RawConfigValue, ConfigDescription, OptionType, OptionConstraints, entities
  Abstractions/     IFcitxSession, FcitxCapabilities
  Fcitx/            FcitxController, FcitxConfigUri
  Transport/Dbus/   D-Bus session, controller client, wire codec, interface XML
Fcitx5.Config.Core.Tests/
scripts/            Native AOT smoke test
```

The transport is `internal`, so consumers never compile against D-Bus types. `Tmds.DBus.Protocol`
still arrives as a transitive dependency; it is an implementation detail, not part of the API.

## License

Apache License 2.0 — see [LICENSE](LICENSE) and [NOTICE](NOTICE). Copyright 2026 Fcitx Contrib.

The package declares `PackageLicenseExpression` `Apache-2.0` and ships both files. Dependencies are
consumed as NuGet packages under their own licenses, not vendored; `Tmds.DBus.Protocol` is MIT. The
interface XML is a signature list read from a running fcitx5 with `gdbus introspect`, not fcitx5
source.
