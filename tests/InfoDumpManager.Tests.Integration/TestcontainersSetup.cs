using System;
using System.Runtime.CompilerServices;

namespace InfoDumpManager.Tests.Integration;

internal static class TestcontainersSetup
{
    [ModuleInitializer]
    public static void Initialize()
    {
        Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");
    }
}
