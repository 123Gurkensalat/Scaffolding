using Vintagestory.API.Common;
using Vintagestory.API.Config;

using System.IO;
using System.Text.Json;

namespace Scaffolding;

public class ModConfigData
{
    public int MaxStability { get; set; } = 6;
}

public class ModConfig
{
    public static ModConfigData Data { get; private set; }
    public static void LoadOrCreate(ICoreAPI api)
    {
        var path = Path.Combine(GamePaths.ModConfig, "scaffolding.json");

        if (api.Side == EnumAppSide.Server)
        {
            if (!File.Exists(path))
            {
                // Save default config if none exists
                Data = new ModConfigData();
                var json = JsonSerializer.Serialize(Data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
                api.Logger.Notification("[Scaffolding] Created default config file at {0}", path);
            }
            else
            {
                var json = File.ReadAllText(path);
                Data = JsonSerializer.Deserialize<ModConfigData>(json);
                api.Logger.Notification("[Scaffolding] Loaded config file from {0}", path);
            }
        }
        else
        {
            Data = new ModConfigData(); // client doesn't need the config usually
        }
    }
}
