using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Scaffolding.Blocks;
using Scaffolding.BlockEntities;
using Scaffolding.Patches;

using HarmonyLib;

namespace Scaffolding;

public class ScaffoldingModSystem : ModSystem
{
    private Harmony harmony;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterBlockClass("scaffolding:scaffolding", typeof(BlockScaffolding));
        api.RegisterBlockEntityClass("scaffolding:scaffolding", typeof(BlockEntityScaffolding));
        PlayerPatches.Api = api;
        harmony = new Harmony(Mod.Info.ModID);
        PlayerPatches.ApplyAll(harmony);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        ModConfig.LoadOrCreate(api);
        var serverChannel = api.Network.RegisterChannel("scaffolding_config").RegisterMessageType<ModConfigData>();
        api.Event.PlayerJoin += player => serverChannel.SendPacket(ModConfig.ServerConfig, player);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        ModConfig.LoadOrCreate(api);
        var clientChannel = api.Network.RegisterChannel("scaffolding_config").RegisterMessageType<ModConfigData>();
        clientChannel.SetMessageHandler<ModConfigData>(config => OnConfigReceived(config, api));
    }

    private void OnConfigReceived(ModConfigData config, ICoreAPI api)
    {
        ModConfig.ClientConfig = config;       
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll(Mod.Info.ModID);
        base.Dispose();
    }
}
