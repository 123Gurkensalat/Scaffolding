using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common.Entities;

namespace Scaffolding.Blocks
{
    internal class EntityFallingScaffolding : EntityBlockFalling
    {
        BlockPos blockPos => new((int)Pos.X, (int)Pos.Y, (int)Pos.Z, Pos.Dimension);

        public EntityFallingScaffolding(Block block, BlockEntity entity, BlockPos pos) : base(block, entity, pos, null, 0, canFallSideways: false, 0) { }

        public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
        {
            base.Initialize(properties, api, InChunkIndex3d);
            this.AfterPhysicsTick += DoAfterPhysicsTick;
        }

        public void DoAfterPhysicsTick()
        {
            var block = World.BlockAccessor.GetBlock(blockPos.Down());
            if (block?.Id == this.Block.Id || (block?.SideIsSolid(blockPos, BlockFacing.UP.Index) ?? false))
            {
                World.BlockAccessor.SetBlock(Block.Id, blockPos);
                Die();
            }
        }

        // disable normal placement behaviour
        public override void OnFallToGround(double motionY) { }
    }
}
