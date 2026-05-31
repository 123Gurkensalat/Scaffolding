using Vintagestory.API.Common;
using Vintagestory.API.Config;

using System.IO;
using System.Text.Json;

using ProtoBuf;

namespace Scaffolding;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class ModConfigData
{
    public int MaxStability { get; set; } = 6;
    public double HorizontalSpeedMult { get; set; } = 1.0;
    public double ClimbUpSpeedMult { get; set; } = 1.0;
    public double ClimbDownSpeedMult { get; set; } = 1.0;
    public double ClimbDistance { get; set; } = 0.5;
}

public class ModConfig
{
    public static ModConfigData ServerConfig;
    public static ModConfigData ClientConfig;
    public static ModConfigData Data(ICoreAPI api) => api.Side == EnumAppSide.Server? ServerConfig : ClientConfig;
    public static void LoadOrCreate(ICoreAPI api)
    {
        var path = Path.Combine(GamePaths.ModConfig, "scaffolding.json");

        if (api.Side == EnumAppSide.Server)
        {
            if (!File.Exists(path))
            {
                // Save default config if none exists
                ServerConfig = new ModConfigData();
                var json = JsonSerializer.Serialize(ServerConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                api.Logger.Notification("[Scaffolding] Created default config file at {0}", path);
            }
            else
            {
                var json = File.ReadAllText(path);
                ServerConfig = JsonSerializer.Deserialize<ModConfigData>(json) ?? new();
                json = JsonSerializer.Serialize(ServerConfig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                api.Logger.Notification("[Scaffolding] Loaded config file from {0}", path);
            }
        }
        else
        {
            ClientConfig = new ModConfigData(); // client doesn't need the config usually
        }
    }
}
