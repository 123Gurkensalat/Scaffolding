using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;

using System.Collections.Generic;

using Scaffolding.Blocks;


namespace Scaffolding.BlockEntities
{
    internal class BlockEntityScaffolding : BlockEntity
    {
        public static int MaxStability = 0;
        public int Stability { get; set; }
        public BlockPos Root = null;
        public bool IsRoot => Root?.Equals(Pos) ?? false;

        private IWorldAccessor World => Api.World;

        public void OnBlockPlaced()
        {
            // if positiv stability, attach to another scaffolding
            // otherwise, block will fall
            var (maxStability, maxStabilityPos) = GetMaxStability();
            if (maxStability < 1)
            {
                var fallingEntity = new EntityFallingScaffolding(Block, this, Pos);
                World.SpawnEntity(fallingEntity);
                return;
            }
            Stability = maxStability;

            // block is root when it has max stability and is placed on solid ground
            bool isRoot = maxStability == MaxStability && GetBlockEntity(Pos.DownCopy()) == null;
            Root = (isRoot) ? Pos : GetBlockEntity(maxStabilityPos).Root;
            PropogateStability();
        }

        /// Checks all neighbors if they can increase their stability when attaching to this block
        /// And calls their PropogateStability()
        private void PropogateStability()
        {
            MarkDirty();
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

        /// Searches for the most stable neighbor
        /// Calculates the resulting stability when attaching to it and returns it
        private (int max, BlockPos maxPos) GetMaxStability()
        {
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

            var reachableNodes = MarkAndGetReachable();

            // check if they have a stable neighbor
            foreach (var node in reachableNodes)
            {
                var (newRoot, newStability) = node.SearchForNewRoot();
                if (newRoot == null || newStability < 1)
                {
                    World.BlockAccessor.BreakBlock(node.Pos, byPlayer);
                }
                else
                {
                    node.Root = newRoot;
                    node.Stability = newStability;
                    node.MarkDirty();
                }
            }
        }

        /// Searches neighbors who are not attached to it or its base.
        /// Returns best possible match or null
        private (BlockPos root, int stability) SearchForNewRoot()
        {
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

        public void OnBlockBelowDestroyed()
        {
            var reachableNodes = MarkAndGetReachable();

            // check if they have a stable neighbor
            foreach (var node in reachableNodes)
            {
                var (newRoot, newStability) = node.SearchForNewRoot();
                if (newRoot != null && newStability > 0)
                {
                    node.Root = newRoot;
                    node.Stability = newStability;
                    node.MarkDirty();
                }
            }

            // get new stability for current Node and update surrounding nodes
            var (stability, position) = GetMaxStability();

            var root = GetBlockEntity(position)?.Root ?? null;
            if (root != null && stability > 0)
            {
                Stability = stability;
                Root = root;
                PropogateStability();
            }
            else
            {
                World.BlockAccessor.BreakBlock(Pos, null);
            }

            // destroy every scaffolding that has to low stability
            foreach (var node in reachableNodes)
            {
                if (node.Root == null || node.Stability < 1)
                {
                    World.BlockAccessor.BreakBlock(node.Pos, null);
                }
            }
        }

        private Stack<BlockEntityScaffolding> MarkAndGetReachable()
        {
            var stack = new Stack<BlockPos>();
            var visited = new HashSet<BlockPos>();

            var nodesToCheck = new Stack<BlockEntityScaffolding>();

            stack.Push(Pos);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                var nodeEntity = GetBlockEntity(node);

                if (visited.Contains(node)) continue;
                visited.Add(node);

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
                        stack.Push(up);
                        nodesToCheck.Push(currentEntity);
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
                        stack.Push(neighbor);
                        nodesToCheck.Push(currentEntity);
                    }
                }
                nodeEntity.Root = null;
                nodeEntity.Stability = 0;
            }
            return nodesToCheck;
        }
        public BlockPos[] Neighbors()
        {
            return new BlockPos[] {
                Pos.NorthCopy(), Pos.EastCopy(), Pos.SouthCopy(), Pos.WestCopy()
            };
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);

            tree.SetInt("stability", Stability);
            if (Root != null)
            {
                tree.SetBlockPos("root", Root);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            Stability = tree.GetInt("stability", 0);
            Root = tree.GetBlockPos("root", null);
        }
    }
}
