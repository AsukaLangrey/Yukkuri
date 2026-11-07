using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// This diagnostic is opt-in and must reject unsafe arguments before world/save creation.
internal static class StationInspectorVerification
{
    internal const string Flag="--station-inspector-check-output";
    internal static void Validate(string[] args,bool legacy=false)
    {
        if(!args.Contains(Flag))return;
        if(legacy||args.Any(a=>a.StartsWith("--m01-",StringComparison.Ordinal)||a is "--legacy" or "--integration"))
            throw new ArgumentException("Station inspector verification only supports the interaction scene.");
        if(DisplayServer.GetName()!="headless")throw new ArgumentException("Station inspector verification is headless only.");
        int output=Array.IndexOf(args,Flag),profile=Array.IndexOf(args,"--interaction-test-profile");
        if(args.Count(a=>a==Flag)!=1||args.Count(a=>a=="--interaction-test-profile")!=1||
            output+1>=args.Length||profile<0||profile+1>=args.Length||
            !Path.IsPathFullyQualified(args[output+1])||!Regex.IsMatch(args[profile+1],@"\A[a-zA-Z0-9_-]{1,64}\z"))
            throw new ArgumentException("Station inspector verification requires one absolute output and one fresh test profile.");
        string? root=System.Environment.GetEnvironmentVariable("YUKKURI_STATION_INSPECTOR_ROOT");
        if(string.IsNullOrWhiteSpace(root)||!Path.IsPathFullyQualified(root))
            throw new ArgumentException("Station inspector verification requires isolated application data.");
        string prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
        bool Within(string? path)=>path!=null&&Path.IsPathFullyQualified(path)&&Path.GetFullPath(path).StartsWith(prefix,StringComparison.OrdinalIgnoreCase);
        foreach(string key in new[]{"APPDATA","LOCALAPPDATA","TEMP","TMP","USERPROFILE","DOTNET_CLI_HOME"})
            if(!Within(System.Environment.GetEnvironmentVariable(key)))throw new ArgumentException("Station inspector verification requires isolated "+key+".");
        string saves=ProjectSettings.GlobalizePath("user://interaction-test-saves/"+args[profile+1]);
        if(!Within(saves)||!Within(args[output+1]))throw new ArgumentException("Station inspector output and saves must remain inside the isolated root.");
        if(Directory.Exists(saves)||File.Exists(saves)||File.Exists(args[output+1])||Directory.Exists(args[output+1]))
            throw new InvalidOperationException("Station inspector verification requires fresh output and profile; prior evidence is preserved.");
        if(args.Any(a=>a.EndsWith("-output",StringComparison.Ordinal)&&a!=Flag))
            throw new ArgumentException("Run the station inspector diagnostic alone.");
    }
}
