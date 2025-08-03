using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

using System.Collections.Generic;

namespace Scaffolding.BlockEntities
{
    internal class BlockEntityScaffolding : BlockEntity
    {
        private const int MaxStability = 4;
        public int Stability { get; set; }

        private List<BlockEntityScaffolding> children = new();
        private bool markedForDestroy = false;

        private IWorldAccessor World => Api.World;
        public void OnBlockPlaced()
        {
            // if positiv stability, attach to another scaffolding
            // otherwise, block will fall
            var (maxStability, maxStabilityPos) = GetMaxStability();
            if (maxStability > 0)
            {
                GetBlockEntity(maxStabilityPos)?.children.Add(this);
                Stability = maxStability;
            }
            else
            {
                // set block to falling
                GetBlockEntity(maxStabilityPos)?.children.Add(this);
                Stability = maxStability;
                //Stability = MaxStability;
            }
        }

        private (int max, BlockPos maxPos) GetMaxStability()
        {
            // world position of neighbors except up
            BlockPos[] neighbors = new BlockPos[] {
                Pos.DownCopy(), Pos.NorthCopy(), Pos.EastCopy(), Pos.SouthCopy(), Pos.WestCopy()
            };

            // set maxStability default to lower scaffoldings stability
            // then search for better alternatives on the sides
            int maxStability = GetBlockEntity(neighbors[0])?.Stability ?? int.MinValue;
            if (GetBlockEntity(neighbors[0])?.markedForDestroy ?? false)
            {
                maxStability = int.MinValue;
            }
            else if (maxStability == int.MinValue
                && World.BlockAccessor.IsSideSolid(neighbors[0].X, neighbors[0].Y, neighbors[0].Z, BlockFacing.UP))
            {
                maxStability = MaxStability;
            }
            int maxStabilityIndex = 0;
            for (int i = 1; i < neighbors.Length; i++)
            {
                var neighbor = GetBlockEntity(neighbors[i]);
                if (maxStability < neighbor?.Stability - 1 && !neighbor.markedForDestroy)
                {
                    maxStability = neighbor.Stability - 1;
                    maxStabilityIndex = i;
                }
            }

            return (maxStability, neighbors[maxStabilityIndex]);
        }

        private BlockEntityScaffolding GetBlockEntity(BlockPos blockPos)
        {
            return World.BlockAccessor.GetBlockEntity<BlockEntityScaffolding>(blockPos);
        }

        // TODO: make this an IEnumerator to destroy it over multiple frames
        public void OnDestroyed(IPlayer byPlayer)
        {
            markedForDestroy = true;

            // get the reference to all children that depend on this block
            List<List<BlockEntityScaffolding>> list = new();
            CollectChildren(0, ref list);

            // go from back to front
            for (int i = list.Count - 1; i >= 0; i--)
            {
                foreach (var child in list[i])
                {
                    // get supporting block that isn't going to be destroyed
                    var (maxStability, maxStabilityPos) = child.GetMaxStability();
                    if (maxStability > 0)
                    {
                        GetBlockEntity(maxStabilityPos)?.children.Add(this);
                        child.Stability = maxStability;
                        child.markedForDestroy = false;
                    }
                    else
                    {
                        World.BlockAccessor.BreakBlock(child.Pos, byPlayer);
                    }
                }
            }
        }

        private void CollectChildren(int depth, ref List<List<BlockEntityScaffolding>> list)
        {
            if (depth >= 10) return;
            if (list.Count == depth)
            {
                list.Add(new());
            }

            Api.Logger.Chat("" + depth);

            list[depth].AddRange(children);
            foreach (var child in children)
            {
                child.markedForDestroy = true;
                child.CollectChildren(depth + 1, ref list);
            }
        }
    }
}
