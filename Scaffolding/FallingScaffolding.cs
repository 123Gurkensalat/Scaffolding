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

    public EntityFallingScaffolding(Block block, BlockEntity entity, BlockPos pos)
        : base(block, entity, pos, null, 0, canFallSideways: false, 0)
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
            TryPlaceBlock(blockPos.Up());
        }
    }

    // disable normal placement behaviour
    public override void OnFallToGround(double motionY)
    {
        TryPlaceBlock(blockPos);
    }

    private void TryPlaceBlock(BlockPos pos)
    {
        if (fallen) return;
        fallen = true;

        string str = "";
        ItemStack itemStack = new(Block, 1);
        BlockSelection blockSelection = new(pos, BlockFacing.UP, Block);
        if (Block.TryPlaceBlock(World, null, itemStack, blockSelection, ref str))
        {
            Die(EnumDespawnReason.Removed);
        }
        else
        {
            Die(EnumDespawnReason.Death);
        }
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer)
    {
        return new ItemStack[] { new ItemStack(world.GetBlock(Block.CodeWithParts("top", "ns")), 1) };
    }
}
