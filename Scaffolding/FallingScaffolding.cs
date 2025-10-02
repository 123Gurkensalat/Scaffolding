using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common.Entities;

using Scaffolding.BlockEntities;

namespace Scaffolding.Blocks;

internal class EntityFallingScaffolding : EntityBlockFalling
{
    int lastY;
    bool fallen = false;
    BlockPos blockPos => new((int)Pos.X, (int)Pos.Y, (int)Pos.Z, Pos.Dimension);

    public EntityFallingScaffolding(Block block, BlockEntity entity, BlockPos pos) : base(block, entity, pos, null, 0, canFallSideways: false, 0)
    {
        lastY = pos.Y;
    }

    public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
    {
        base.Initialize(properties, api, InChunkIndex3d);
        this.AfterPhysicsTick += DoAfterPhysicsTick;
    }

    public void DoAfterPhysicsTick()
    {
        if (lastY != (int)Pos.Y)
        {
            OnBlockPosChanged();
            lastY = (int)Pos.Y;
        }
    }

    private void OnBlockPosChanged()
    {
        if (World.BlockAccessor.GetBlockEntity<BlockEntityScaffolding>(blockPos) != null)
        {
            World.BlockAccessor.SetBlock(Block.Id, blockPos.Up());
            World.BlockAccessor.TriggerNeighbourBlockUpdate(blockPos.Up());
            Die(EnumDespawnReason.Removed);
        }
    }

    // disable normal placement behaviour
    public override void OnFallToGround(double motionY)
    {
        if (fallen) return;
        fallen = true;
        var block = World.BlockAccessor.GetBlock(blockPos);
        string str = "";
        if (Block.CanPlaceBlock(World, null, new BlockSelection(blockPos, BlockFacing.UP, Block), ref str))
        {
            World.BlockAccessor.SetBlock(Block.Id, blockPos);
            World.BlockAccessor.TriggerNeighbourBlockUpdate(blockPos);
            Die(EnumDespawnReason.Removed);
        }
        else
        {
            Die(EnumDespawnReason.Death);
        }
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer)
    {
        return new ItemStack[] { new ItemStack(Block, 1) };
    }
}
