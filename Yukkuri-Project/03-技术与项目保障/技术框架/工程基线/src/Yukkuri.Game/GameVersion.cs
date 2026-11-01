// The project Version property is the single source for the visible build version.
// Read the compiled assembly so exported builds keep their own version identity.
public static class GameVersion
{
    public static readonly string Display="v"+(typeof(GameVersion).Assembly.GetName().Version?.ToString(3)??"未标记");
}
