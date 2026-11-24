#:project ../Fcitx5.Config.Core/Fcitx5.Config.Core.csproj
#:property PublishAot=true
#:property TrimmerSingleWarn=false

// Native AOT smoke test: the library's transport is exercised from a published native binary.
// Run it through scripts/aot-smoke.sh, which checks the publish log for trimming diagnostics.
//
// Exit codes:
//   0  success
//   1  the library failed
//   2  fcitx5 is not running (the transport itself is fine)

using Fcitx5.Config.Core.Fcitx;

try
{
    await using var controller = await FcitxController.ConnectDbusAsync();

    if (!await WaitForAvailabilityAsync(controller))
    {
        Console.WriteLine("SKIP: fcitx5 is not running on the session bus.");
        return 2;
    }

    var state = await controller.GetStateAsync();
    Console.WriteLine($"state: {state}");

    var addons = await controller.GetAddonsAsync();
    Console.WriteLine($"addons: {addons.Count}");

    var inputMethods = await controller.GetInputMethodsAsync();
    Console.WriteLine($"input methods: {inputMethods.Count}");

    var groups = await controller.GetInputMethodGroupsAsync();
    Console.WriteLine($"groups: {string.Join(", ", groups)}");

    var global = await controller.GetGlobalConfigAsync();
    Console.WriteLine($"global groups: {global.Description.Types.Length}");

    var triggerKeys = global.FindOption("TriggerKeys");
    Console.WriteLine(
        triggerKeys is null
            ? "TriggerKeys: absent"
            : $"TriggerKeys: {triggerKeys.Type.Raw}, default={triggerKeys.DefaultValue?.ToStringList().Count.ToString() ?? "none"}");

    var current = await controller.GetInputMethodGroupAsync();
    Console.WriteLine($"current group: {current.Name} ({current.Items.Length} items)");

    Console.WriteLine("AOT SMOKE PASSED");
    return 0;
}
catch (Exception exception)
{
    Console.WriteLine($"FAILED: {exception.GetType().FullName}: {exception.Message}");
    return 1;
}

static async Task<bool> WaitForAvailabilityAsync(FcitxController controller)
{
    for (var attempt = 0; attempt < 40; attempt++)
    {
        if (controller.IsAvailable)
        {
            return true;
        }

        await Task.Delay(50);
    }

    return controller.IsAvailable;
}
