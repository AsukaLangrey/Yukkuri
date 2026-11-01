# Inspect only the specified process. No machine-wide configuration is changed.
if(-not ('YukkuriProcessAffinityV1' -as [type])){
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class YukkuriProcessAffinityV1 {
    [DllImport("kernel32.dll", SetLastError=true)]
    public static extern bool GetProcessAffinityMask(IntPtr handle, out UIntPtr processMask, out UIntPtr systemMask);
}
'@
}
function Get-YukkuriProcessAffinity([Diagnostics.Process]$Process){
    $processMask=[UIntPtr]::Zero;$systemMask=[UIntPtr]::Zero
    if(![YukkuriProcessAffinityV1]::GetProcessAffinityMask($Process.Handle,[ref]$processMask,[ref]$systemMask)){
        throw "Cannot inspect process affinity: $([Runtime.InteropServices.Marshal]::GetLastWin32Error())"
    }
    return [ordered]@{process_mask=$processMask.ToUInt64();system_mask=$systemMask.ToUInt64()}
}
