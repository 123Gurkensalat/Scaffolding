using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Scaffolding.Blocks
{
    public class BlockScaffolding : Block
    {
        private float last_used = int.MinValue; // in seconds
        private const float dTick = 0.5f; // time between ticks in seconds

        public override bool OnBlockInteractStep(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (secondsUsed - last_used < dTick) return false;
            last_used = secondsUsed;

            bool shift_pressed = byPlayer.Entity.Controls.ShiftKey;
            if (shift_pressed)
            {
                TryAddScaffolding(world, byPlayer, blockSel.Position);
            }
            else
            {
                TryRemoveScaffolding(world, byPlayer, blockSel.Position);
            }

            return false;
        }

        public override bool OnBlockInteractCancel(float secondsUsed, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, EnumItemUseCancelReason cancelReason)
        {
            last_used = int.MinValue;
            return base.OnBlockInteractCancel(secondsUsed, world, byPlayer, blockSel, cancelReason);
        }

        /// </summary>
        /// Adds scaffolding block on top of the highest scaffolding
        /// Fails if player has no scaffolding left
        /// </summary>
        /// <param name="world">The world this block is interacted from</param>
        /// <param name="byPlayer">The player who performed the action</param>
        /// <param name="fromPosition">Position of the scaffolding to start from
        private void TryAddScaffolding(IWorldAccessor world, IPlayer byPlayer, BlockPos fromPosition)
        {
            // check if player holds scaffolding in his hand
            if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack.Id != Id) return;

            // increment y (world up) untill there is no more scaffolding
            BlockPos current_pos = fromPosition.Copy();
            do
            {
                current_pos.Y += 1;
            } while (world.BlockAccessor.GetBlockId(current_pos) == Id);

            // check if block above scaffolding is air
            if (world.BlockAccessor.GetBlockId(current_pos) != 0) return;

            // check if is inside world borders
            if (!world.BlockAccessor.IsValidPos(current_pos)) return;

            // place scaffolding and remove one from the players inventory
            world.BlockAccessor.SetBlock(Id, current_pos);
            byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
        }

        /// <summary>
        /// Removes scaffolding from the top
        /// Breaks the block instead if the player has no free inventory space
        /// </summary>
        /// <param name="world">The world this block is interacted from</param>
        /// <param name="byPlayer">The player who performed the action</param>
        /// <param name="fromPosition">Position of the scaffolding to start from</param>
        private void TryRemoveScaffolding(IWorldAccessor world, IPlayer byPlayer, BlockPos fromPosition)
        {
            ItemStack scaffolding_stack = new ItemStack(new BlockScaffolding());

            // give player scaffolding item. When inventory is full, return
            if (!byPlayer.InventoryManager.TryGiveItemstack(scaffolding_stack)) return;

            // find to top most scaffolding block
            BlockPos current_pos = fromPosition.Copy();
            do
            {
                current_pos.Y += 1;
            } while (world.BlockAccessor.GetBlockId(current_pos) == Id);
            current_pos.Y -= 1;

            // DESTROY IT!
            world.BlockAccessor.SetBlock(0, current_pos);
        }

    }
}
