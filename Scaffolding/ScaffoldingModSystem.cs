using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Common.Entities;
using Scaffolding.Blocks;
using Scaffolding.BlockEntities;
using Vintagestory.API.Client;

namespace Scaffolding;

public class ScaffoldingModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterBlockClass("scaffolding:scaffolding", typeof(BlockScaffolding));
        api.RegisterBlockEntityClass("scaffolding:scaffolding", typeof(BlockEntityScaffolding));
        api.RegisterEntityBehaviorClass("scaffolding:climb", typeof(PlayerPhysicsHandler));
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);

        // load mod config
        ModConfig.LoadOrCreate(api);
        BlockEntityScaffolding.MaxStability = ModConfig.Data.MaxStability;

        api.Event.PlayerJoin += (IServerPlayer player) => AddPlayerBehavior(player.Entity, api);
    }


    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);

        api.Event.OnEntitySpawn += (Entity entity) => AddPlayerBehavior(entity);
        api.Event.OnEntityLoaded += (Entity entity) => AddPlayerBehavior(entity);
    }

    private void AddPlayerBehavior(Entity entity, ICoreServerAPI api = null)
    {
        if (entity is EntityPlayer player)
        {
            player.AddBehavior(new PlayerPhysicsHandler(player, api));
        }
    }
}
