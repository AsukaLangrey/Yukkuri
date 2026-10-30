using System;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;
using Yukkuri.Simulation;

// Debug output includes transient state that deliberately must not survive a
// save/load. These private options never change SaveStore's persistence contract.
public static class DebugStateSnapshot
{
    private static readonly JsonSerializerOptions Options=CreateOptions();

    public static JsonElement Capture(object? source)=>JsonSerializer.SerializeToElement(source,Options);

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver=new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(IncludeTransientState);
        return new()
        {
            WriteIndented=true,
            IncludeFields=true, // Includes the components of transient Quaternion/Vector values.
            Encoder=JavaScriptEncoder.Create(UnicodeRanges.All),
            TypeInfoResolver=resolver,
            Converters={new JsonStringEnumConverter()}
        };
    }

    private static void IncludeTransientState(JsonTypeInfo contract)
    {
        if(contract.Kind!=JsonTypeInfoKind.Object||contract.Type.Assembly!=typeof(WorldState).Assembly)return;
        foreach(var property in contract.Type.GetProperties(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
        {
            if(property.GetMethod==null||property.GetIndexParameters().Length!=0||
                property.GetCustomAttribute<JsonIgnoreAttribute>()==null)continue;
            string name=property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name??property.Name;
            var ignored=contract.Properties.FirstOrDefault(candidate=>candidate.Name==name);
            if(ignored!=null)contract.Properties.Remove(ignored);
            // A fresh read-only contract avoids retaining JsonIgnore's disabled
            // getter/predicate. The currently annotated getters only read state.
            var visible=contract.CreateJsonPropertyInfo(property.PropertyType,name);
            visible.Get=instance=>property.GetValue(instance);
            contract.Properties.Add(visible);
        }
    }
}
