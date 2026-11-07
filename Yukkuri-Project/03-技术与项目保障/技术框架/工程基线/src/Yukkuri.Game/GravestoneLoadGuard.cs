using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

// Opt-in diagnostic only. Reject unsafe arguments before any world or SaveStore exists.
internal static class GravestoneLoadGuard
{
    internal const string Flag="--gravestone-load-audit-output";
    internal static string Root { get; private set; }="";
    internal static void Validate(string[] args,bool legacy=false)
    {
        if(!args.Contains(Flag))return;
        int o=Array.IndexOf(args,Flag),p=Array.IndexOf(args,"--interaction-test-profile");
        if(legacy||DisplayServer.GetName()!="headless"||args.Length!=4||o<0||p<0||o+1>=args.Length||p+1>=args.Length||
           args.Count(a=>a==Flag)!=1||args.Count(a=>a=="--interaction-test-profile")!=1||
           !System.Text.RegularExpressions.Regex.IsMatch(args[p+1],@"\Agrave-load-[a-z0-9-]{1,32}\z"))
            throw new ArgumentException("GRAVE_GUARD_REJECT: requires InteractionRoom, only diagnostic output and isolated profile, headless.");
        string root=System.Environment.GetEnvironmentVariable("YUKKURI_GRAVE_TEST_ROOT")??"";
        if(!Path.IsPathFullyQualified(root)||!Directory.Exists(root))
            throw new ArgumentException("GRAVE_GUARD_REJECT: explicit existing isolated test root required.");
        Root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string output=Path.GetFullPath(args[o+1]),directory=Path.GetDirectoryName(output)!;
        string save=Path.GetFullPath(ProjectSettings.GlobalizePath("user://interaction-test-saves/"+args[p+1]));
        string processPrefix=Root+Path.DirectorySeparatorChar+"process-";
        if(!Path.IsPathFullyQualified(args[o+1])||
           !Path.GetFileName(directory).StartsWith("runtime-",StringComparison.Ordinal)||Path.GetDirectoryName(directory)!=Root||
           Directory.Exists(directory)||File.Exists(directory)||File.Exists(output)||Directory.Exists(save)||File.Exists(save)||
           !save.StartsWith(processPrefix,StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("GRAVE_GUARD_REJECT: requires fresh in-root save and output directories.");
        foreach(string key in new[]{"APPDATA","LOCALAPPDATA","TEMP","TMP","USERPROFILE","DOTNET_CLI_HOME"})
            if(!(System.Environment.GetEnvironmentVariable(key)??"").StartsWith(processPrefix,StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("GRAVE_GUARD_REJECT: environment is not isolated: "+key);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory,"guard.json"),JsonSerializer.Serialize(new{guardBeforeWorldAndSaveStore=true}));
        GD.Print("GRAVE_GUARD_ACCEPT");
    }
}