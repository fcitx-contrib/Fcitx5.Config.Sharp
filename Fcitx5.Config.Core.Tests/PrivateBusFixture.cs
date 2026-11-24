using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace Fcitx5.Config.Core.Tests;

/// <summary>
/// A private D-Bus session bus for tests, so they never touch the user's daemon.
/// </summary>
/// <remarks>
/// The bus is started once per test collection. Tests that need a service on it own the well-known
/// name <c>org.fcitx.Fcitx5</c> for the duration of the test, which is why the collection is
/// serialised.
/// </remarks>
public sealed class PrivateBusFixture : IDisposable
{
    private readonly Process? _daemon;
    private readonly string? _directory;

    public PrivateBusFixture()
    {
        if (!OperatingSystem.IsLinux() || !File.Exists("/usr/bin/dbus-daemon"))
        {
            return;
        }

        _directory = Path.Combine(Path.GetTempPath(), "fcitx5-core-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);

        var configPath = Path.Combine(_directory, "bus.conf");
        File.WriteAllText(
            configPath,
            $"""
            <!DOCTYPE busconfig PUBLIC "-//freedesktop//DTD D-Bus Bus Configuration 1.0//EN" "http://www.freedesktop.org/standards/dbus/1.0/busconfig.dtd">
            <busconfig>
              <type>session</type>
              <listen>unix:tmpdir={_directory}</listen>
              <auth>EXTERNAL</auth>
              <policy context="default">
                <allow send_destination="*" eavesdrop="true"/>
                <allow eavesdrop="true"/>
                <allow own="*"/>
              </policy>
            </busconfig>
            """);

        var startInfo = new ProcessStartInfo(
            "dbus-daemon",
            $"--config-file={configPath} --print-address=1 --nofork")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        try
        {
            _daemon = Process.Start(startInfo);
            Address = _daemon?.StandardOutput.ReadLine();
        }
        catch (Exception)
        {
            _daemon = null;
            Address = null;
        }
    }

    /// <summary>The bus address, or <see langword="null"/> when no bus could be started.</summary>
    public string? Address { get; }

    public void Dispose()
    {
        try
        {
            if (_daemon is { HasExited: false })
            {
                _daemon.Kill();
                _daemon.WaitForExit(2000);
            }
        }
        catch (Exception)
        {
            // Teardown must not turn a green run red.
        }

        _daemon?.Dispose();

        try
        {
            if (_directory is not null && Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
        catch (Exception)
        {
            // The temporary directory may already be gone with the daemon.
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PrivateBusCollection : ICollectionFixture<PrivateBusFixture>
{
    public const string Name = "private-dbus";
}
