using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// Only the new speed check opts into this contract. Validate before scene routing
// and before any SaveStore; all unrelated historical entry contracts stay unchanged.
internal static class InteractionTestArguments
{
    private const string OutputFlag="--courtyard-speed-check-output";
    private const string ProfileFlag="--interaction-test-profile";
    internal static string? Validate(string[] args,bool legacyEntry=false)
    {
        if(!args.Contains(OutputFlag))return null;
        if(legacyEntry||args.Any(a=>a.StartsWith("--m01-",StringComparison.Ordinal)||a is "--legacy" or "--integration"))
            throw new ArgumentException("Courtyard speed checks cannot route to a legacy scene.");
        if(args.Any(a=>a.StartsWith("--",StringComparison.Ordinal)&&a!=OutputFlag&&a!=ProfileFlag&&a!="--mute"))
            throw new ArgumentException("Courtyard speed checks cannot be combined with other user flags.");
        if(args.Count(a=>a==ProfileFlag)!=1||args.Count(a=>a==OutputFlag)!=1||args.Count(a=>a=="--mute")>1)
            throw new ArgumentException("Speed checks require exactly one isolated profile and one output.");
        int profile=Array.IndexOf(args,ProfileFlag);
        if(profile+1>=args.Length||!Regex.IsMatch(args[profile+1],@"\A[a-zA-Z0-9_-]{1,64}\z"))
            throw new ArgumentException("Interaction test profile must be a short alphanumeric name.");
        int outputIndex=Array.IndexOf(args,OutputFlag);
        if(outputIndex+1>=args.Length||!Path.IsPathFullyQualified(args[outputIndex+1]))
            throw new ArgumentException("Courtyard speed check output must be an absolute path.");
        string output=Path.GetFullPath(args[outputIndex+1]);
        if(File.Exists(output)||Directory.Exists(output))
            throw new ArgumentException("Courtyard speed check output must be fresh.");
        return args[profile+1];
    }
}