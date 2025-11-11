using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Infrastructure.Helpers;

internal static class PathHelpers
{
    internal static string GetProcessFilename(string processName) => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? $"{processName}.exe" : processName;
}
