using Vintagestory.API.Common;

using Scaffolding.Blocks;
using Scaffolding.BlockEntities;

namespace Scaffolding
{
    public class ScaffoldingModSystem : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass(Mod.Info.ModID + ".scaffolding", typeof(BlockScaffolding));
            api.RegisterBlockEntityClass(Mod.Info.ModID + ".scaffolding", typeof(BlockEntityScaffolding));
        }
    }
}
