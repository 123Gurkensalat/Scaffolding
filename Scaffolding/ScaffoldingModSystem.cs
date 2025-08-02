using Vintagestory.API.Common;

using Scaffolding.Blocks;

namespace Scaffolding
{
    public class ScaffoldingModSystem : ModSystem
    {
        // Called on server and client
        public override void Start(ICoreAPI api)
        {
            Mod.Logger.Chat("Scaffolding loaded.");
            api.RegisterBlockClass(Mod.Info.ModID + ".scaff", typeof(BlockScaffolding));
        }
    }
}
