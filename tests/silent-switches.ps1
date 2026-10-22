# Compiles just VendorNet.SilentSwitches (the rest of VendorNet needs Windows) so it can be tested anywhere
$src = Get-Content ../src/VendorNet.cs -Raw
$i = $src.IndexOf('        public static string SilentSwitches'); $j = $src.IndexOf('        /// <summary>Runs one installer', $i)
Add-Type -TypeDefinition ("using System; namespace DriverUpdater { public static class VendorNet {`n" + $src.Substring($i, $j - $i) + "}}")
