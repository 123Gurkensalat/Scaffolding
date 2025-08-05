using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

using System.Collections.Generic;


namespace Scaffolding.BlockEntities
{
    internal class BlockEntityScaffolding : BlockEntity
    {
        private const int MaxStability = 4;
        public int Stability { get; set; }
        public BlockPos Root = null;

        private IWorldAccessor World => Api.World;

        public void OnBlockPlaced()
        {
            // if positiv stability, attach to another scaffolding
            // otherwise, block will fall
            var (maxStability, maxStabilityPos) = GetMaxStability();
            if (maxStability < 1)
            {
                var fallingEntity = new EntityBlockFalling(Block, this, Pos, null, 0, canFallSideways: false, 0);
                World.SpawnEntity(fallingEntity);
                return;
            }
            Stability = maxStability;

            // block is root when it has max stability and is placed on solid ground
            bool isRoot = maxStability == MaxStability && GetBlockEntity(Pos.DownCopy()) == null;
            Root = (isRoot) ? Pos : GetBlockEntity(maxStabilityPos).Root;
            PropogateStability();
        }

        private void PropogateStability()
        {
            var currentEntity = GetBlockEntity(Pos.UpCopy());
            if (currentEntity?.Stability <= Stability)
            {
                currentEntity.Stability = Stability;
                currentEntity.Root = Root;
                currentEntity.PropogateStability();
            }

            foreach (var neighbor in Neighbors())
            {
                currentEntity = GetBlockEntity(neighbor);
                if (currentEntity?.Stability < Stability - 1)
                {
                    currentEntity.Stability = Stability - 1;
                    currentEntity.Root = Root;
                    currentEntity.PropogateStability();
                }
            }
        }

        private (int max, BlockPos maxPos) GetMaxStability()
        {
            // set maxStability default to lower scaffoldings stability
            // then search for better alternatives on the sides
            var currentEntity = GetBlockEntity(Pos.DownCopy());
            int maxStability = int.MinValue;
            if (currentEntity != null)
            {
                maxStability = currentEntity.Stability;
            }
            else if (World.BlockAccessor.IsSideSolid(Pos.X, Pos.Y - 1, Pos.Z, BlockFacing.UP))
            {
                maxStability = MaxStability;
            }

            BlockPos maxPos = Pos.DownCopy();
            foreach (var neighbor in Neighbors())
            {
                currentEntity = GetBlockEntity(neighbor);
                if (currentEntity == null) continue;

                if (maxStability < currentEntity.Stability - 1)
                {
                    maxStability = currentEntity.Stability - 1;
                    maxPos = neighbor.Copy();
                }
            }

            return (maxStability, maxPos);
        }

        private BlockEntityScaffolding GetBlockEntity(BlockPos blockPos)
        {
            return World.BlockAccessor.GetBlockEntity<BlockEntityScaffolding>(blockPos);
        }

        // TODO: make this an IEnumerator to destroy it over multiple frames
        public void OnDestroy(IPlayer byPlayer)
        {
            if (Root == null) return;

            var stack = new Stack<(BlockPos, bool)>();
            var visited = new HashSet<BlockPos>();

            stack.Push((Pos, false));

            while (stack.Count > 0)
            {
                var (node, processed) = stack.Pop();
                var nodeEntity = GetBlockEntity(node);

                if (processed)
                {
                    var (newRoot, newStability) = nodeEntity.SearchForNewRoot();
                    if (newRoot == null || newStability < 1)
                    {
                        World.BlockAccessor.BreakBlock(node, byPlayer);
                    }
                    else
                    {
                        nodeEntity.Root = newRoot;
                        nodeEntity.Stability = newStability;
                    }
                    continue;
                }

                if (visited.Contains(node)) continue;
                visited.Add(node);
                stack.Push((node, true));

                BlockEntityScaffolding currentEntity;
                BlockPos up = node.UpCopy();
                if (!visited.Contains(up))
                {
                    currentEntity = GetBlockEntity(up);
                    if (currentEntity == null)
                    {
                        visited.Add(up);
                    }
                    else if (currentEntity.Stability == nodeEntity.Stability)
                    {
                        stack.Push((up, false));
                    }
                }

                foreach (var neighbor in nodeEntity.Neighbors())
                {
                    if (visited.Contains(neighbor)) continue;

                    currentEntity = GetBlockEntity(neighbor);
                    if (currentEntity == null)
                    {
                        visited.Add(neighbor);
                    }
                    else if (currentEntity.Stability < nodeEntity.Stability
                            && currentEntity.Root.Equals(nodeEntity.Root))
                    {
                        stack.Push((neighbor, false));
                    }
                }
                nodeEntity.Root = null;
            }
        }

        private (BlockPos root, int stability) SearchForNewRoot()
        {
            // set maxStability default to lower scaffoldings stability
            // then search for better alternatives on the sides
            var currentEntity = GetBlockEntity(Pos.DownCopy());
            int newStability = int.MinValue;
            BlockPos newRoot = null;
            if (currentEntity != null && currentEntity.Root != null)
            {
                newStability = currentEntity.Stability;
                newRoot = GetBlockEntity(Pos.DownCopy()).Root;
            }

            foreach (var neighbor in Neighbors())
            {
                currentEntity = GetBlockEntity(neighbor);
                if (currentEntity == null) continue;
                if (currentEntity.Root == null) continue;

                if (newStability < currentEntity.Stability - 1)
                {
                    newStability = currentEntity.Stability - 1;
                    newRoot = GetBlockEntity(neighbor).Root;
                }
            }
            return (newRoot, newStability);
        }

        public BlockPos[] Neighbors()
        {
            return new BlockPos[] {
                Pos.NorthCopy(), Pos.EastCopy(), Pos.SouthCopy(), Pos.WestCopy()
            };
        }
    }
}
