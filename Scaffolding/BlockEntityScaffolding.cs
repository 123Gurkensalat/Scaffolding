using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;

using System.Collections.Generic;
using System;

using Scaffolding.Blocks;


namespace Scaffolding.BlockEntities;
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

    /// Checks all neighbours if they can increase their stability when attaching to this block
    /// And calls their PropogateStability()
    private void PropogateStability()
    {
        MarkDirty();
        var currentEntity = GetBlockEntity(Pos.UpCopy());
        if (currentEntity?.Stability < Stability)
        {
            currentEntity.Stability = Stability;
            currentEntity.Root = Root;
            currentEntity.PropogateStability();
        }

        foreach (var neighbour in Neighbours())
        {
            currentEntity = GetBlockEntity(neighbour);
            if (currentEntity?.Stability < Stability - 1)
            {
                currentEntity.Stability = Stability - 1;
                currentEntity.Root = Root;
                currentEntity.PropogateStability();
            }
        }
    }

    /// Searches for the most stable neighbour
    /// Calculates the resulting stability when attaching to it and returns it
    private (int max, BlockPos maxPos) GetMaxStability()
    {
        var currentEntity = GetBlockEntity(Pos.DownCopy());
        int maxStability = int.MinValue;
        BlockPos maxPos = null;
        if (currentEntity != null)
        {
            maxStability = currentEntity.Stability;
            maxPos = currentEntity.Pos;
        }
        else if (World.BlockAccessor.IsSideSolid(Pos.X, Pos.Y - 1, Pos.Z, BlockFacing.UP))
        {
            maxStability = MaxStability;
            maxPos = Pos.DownCopy();
        }

        foreach (var neighbour in Neighbours())
        {
            currentEntity = GetBlockEntity(neighbour);
            if (currentEntity == null) continue;

            if (maxStability < currentEntity.Stability - 1)
            {
                maxStability = currentEntity.Stability - 1;
                maxPos = neighbour;
            }
        }

        return (maxStability, maxPos);
    }

    private BlockEntityScaffolding GetBlockEntity(BlockPos blockPos)
    {
        return World.BlockAccessor.GetBlockEntity<BlockEntityScaffolding>(blockPos);
    }

    // TODO: make this an IEnumerator to destroy it over multiple frames
    public bool OnDestroy(IPlayer byPlayer)
    {
        if (Root == null) return true;

        var reachableNodes = UpdateReachable(() =>
        {
            Root = null;
            World.BlockAccessor.BreakBlock(Pos, byPlayer);
        });

        // check if they have a stable neighbour
        foreach (var node in reachableNodes)
        {
            if (node.Root == null || node.Stability < 1)
            {
                World.BlockAccessor.BreakBlock(node.Pos, byPlayer);
            }
        }
        return false;
    }

    public void OnBlockBelowDestroyed()
    {
        var reachableNodes = UpdateReachable();

        // get new stability for current Node and update surrounding nodes
        var (stability, position) = GetMaxStability();
        if (stability > 0)
        {
            var root = GetBlockEntity(position)?.Root ?? null;
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

    private List<BlockEntityScaffolding> UpdateReachable(Action afterMark = null)
    {
        var queue = new Queue<BlockPos>();
        var visited = new HashSet<BlockPos>();
        var reachableNodes = new List<BlockEntityScaffolding>();
        var neighbourNodes = new List<BlockEntityScaffolding>();

        queue.Enqueue(Pos);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            var nodeEntity = GetBlockEntity(node);

            if (visited.Contains(node)) continue;
            visited.Add(node);

            if (!node.Equals(Pos))
            {
                reachableNodes.Add(nodeEntity);
            }

            // add above block to reachable
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
                    queue.Enqueue(up);
                }
            }

            // add below block to neighbour
            BlockPos down = node.DownCopy();
            if (!visited.Contains(down))
            {
                currentEntity = GetBlockEntity(down);
                if (currentEntity == null)
                {
                    visited.Add(down);
                }
                else
                {
                    neighbourNodes.Add(currentEntity);
                }
            }

            // add side blocks to reachable and neighbour
            foreach (var neighbour in nodeEntity.Neighbours())
            {
                if (visited.Contains(neighbour)) continue;

                currentEntity = GetBlockEntity(neighbour);
                if (currentEntity == null)
                {
                    visited.Add(neighbour);
                }
                else if (currentEntity.Stability < nodeEntity.Stability
                        && currentEntity.Root.Equals(nodeEntity.Root))
                {
                    queue.Enqueue(neighbour);
                }
                else if (currentEntity.Stability >= nodeEntity.Stability
                        || !currentEntity.Root.Equals(nodeEntity.Root))
                {
                    neighbourNodes.Add(currentEntity);
                    visited.Add(neighbour);
                }
            }

            nodeEntity.Root = null;
            nodeEntity.Stability = 0;
        }

        afterMark?.Invoke();

        foreach (var node in neighbourNodes)
        {
            node.PropogateStability();
        }

        return reachableNodes;
    }
    public BlockPos[] Neighbours()
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
