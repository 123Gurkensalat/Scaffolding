using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

using System;
using System.Collections.Generic;

using Scaffolding.BlockEntities;

namespace Scaffolding.Blocks
{
    internal class BlockScaffolding : Block
    {
        public override void OnBeingLookedAt(IPlayer byPlayer, BlockSelection blockSel, bool firstTick)
        {
            if (firstTick)
            {
                api.Logger.Chat("" + GetBlockEntity(blockSel.Position).Stability);
            }
            base.OnBeingLookedAt(byPlayer, blockSel, firstTick);
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // when shift + rmb scaffolding -> normal block behaviour
            bool shiftPressed = byPlayer.Entity.Controls.ShiftKey;
            if (shiftPressed) return false;

            // if not holding scaffolding -> normal behaviour
            if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Id != Id) return false;

            // when rmb on top-side of scaffolding -> add scaffolding to player look direction
            bool isTopSide = blockSel.Face == BlockFacing.UP;
            if (isTopSide)
            {
                TryAddScaffoldingToSide(world, byPlayer, blockSel.Position);
                return true;
            }

            // when rmb scaffolding -> add scaffolding to top
            TryAddScaffolding(world, byPlayer, blockSel.Position);

            return true;
        }

        private void TryAddScaffolding(IWorldAccessor world, IPlayer byPlayer, BlockPos fromPosition)
        {
            Vec3i up = new Vec3i(0, 1, 0);
            TryAddScaffoldingToDirection(world, byPlayer, fromPosition, up);
        }

        private void TryAddScaffoldingToSide(IWorldAccessor world, IPlayer byPlayer, BlockPos fromPosition)
        {
            // get direction based of camera rotation
            float yaw = byPlayer.Entity.Pos.Yaw * GameMath.RAD2DEG;
            int i = GameMath.Mod((int)Math.Round(yaw / 90f), 4);
            Vec3i dir = i switch
            {
                0 => new Vec3i(0, 0, 1),  // North
                1 => new Vec3i(1, 0, 0),  // East
                2 => new Vec3i(0, 0, -1), // South
                3 => new Vec3i(-1, 0, 0), // West
                _ => new Vec3i(0, 0, 0)
            };

            TryAddScaffoldingToDirection(world, byPlayer, fromPosition, dir);
        }

        private void TryAddScaffoldingToDirection(IWorldAccessor world, IPlayer byPlayer, BlockPos fromPosition, Vec3i direction)
        {
            // increment y (world up) untill there is no more scaffolding
            BlockPos current_pos = fromPosition.Copy();
            do
            {
                current_pos.Add(direction);
            } while (world.BlockAccessor.GetBlockId(current_pos) == Id);

            // check if is inside world borders
            if (!world.BlockAccessor.IsValidPos(current_pos)) return;

            // check if block above scaffolding is air
            if (world.BlockAccessor.GetBlockId(current_pos) != 0) return;

            // place scaffolding and remove one from the players inventory
            world.BlockAccessor.SetBlock(Id, current_pos);
            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                byPlayer.InventoryManager.ActiveHotbarSlot.TakeOut(1);
            }
        }

        public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
        {
            // if positiv stability, attach to another scaffolding
            // otherwise, block will fall
            base.OnBlockPlaced(world, blockPos, byItemStack);
            GetBlockEntity(blockPos).OnBlockPlaced();
        }

        private BlockEntityScaffolding GetBlockEntity(BlockPos blockPos)
        {
            return api.World.BlockAccessor.GetBlockEntity<BlockEntityScaffolding>(blockPos);
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
        {
            GetBlockEntity(pos)?.OnDestroyed(byPlayer);
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }
    }
}
