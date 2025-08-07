using Vintagestory.API.Common;

using Scaffolding.Blocks;
using Scaffolding.BlockEntities;
using Vintagestory.API.Server;

namespace Scaffolding
{
    public class ScaffoldingModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockClass(Mod.Info.ModID + ".scaffolding", typeof(BlockScaffolding));
            api.RegisterBlockEntityClass(Mod.Info.ModID + ".scaffolding", typeof(BlockEntityScaffolding));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            ModConfig.LoadOrCreate(api);
            BlockEntityScaffolding.MaxStability = ModConfig.Data.MaxStability;
        }
    }
}
