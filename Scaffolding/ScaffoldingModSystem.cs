using Vintagestory.API.Common;
using Vintagestory.API.Server;
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

        // load mod config
        ModConfig.LoadOrCreate(api);
        BlockScaffolding.MaxStability = ModConfig.Data.MaxStability;
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll(Mod.Info.ModID);
        base.Dispose();
    }
}
