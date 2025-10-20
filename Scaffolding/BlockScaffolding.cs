using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

using System.Collections.Generic;
using System;

using Scaffolding.BlockEntities;

namespace Scaffolding.Blocks;

internal class BlockScaffolding : Block
{
    private static int? _maxStability = null;
    public static int MaxStability
    {
        get
        {
            _maxStability ??= 6;
            return _maxStability ?? 6;
        }
        set { _maxStability ??= value; }
    }

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        var (_, maxStabilityPos) = GetMaxStability(blockSel.Position);
        var block = api.World.GetBlock(GetCode(blockSel.Position, maxStabilityPos, byPlayer));
        if (block.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
        {
            block.DoPlaceBlock(world, byPlayer, blockSel, itemstack);
            world.BlockAccessor.TriggerNeighbourBlockUpdate(blockSel.Position);
            return true;
        }
        return false;
    }

    public override void OnBlockPlaced(IWorldAccessor world, BlockPos pos, ItemStack byItemStack = null)
    {
        base.OnBlockPlaced(world, pos, byItemStack);

        // if positiv stability, attach to another scaffolding
        // otherwise, block will fall
        var (maxStability, maxStabilityPos) = GetMaxStability(pos);
        var entity = GetBlockEntity(pos);
        if (maxStability < 1)
        {
            var fallingEntity = new EntityFallingScaffolding(this, entity, pos);
            api.World.SpawnEntity(fallingEntity);
            return;
        }

        entity.Stability = maxStability;
        // block is root when it has max stability and is placed on solid ground
        bool isRoot = maxStability == MaxStability && GetBlockEntity(pos.DownCopy()) == null;
        entity.Root = isRoot ? pos : GetBlockEntity(maxStabilityPos).Root;

        // Updates surrounding stability and root
        PropogateStability(entity, true);
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        // when shift + rmb scaffolding -> normal block behaviour
        if (byPlayer.Entity.Controls.ShiftKey) return false;

        // if not holding scaffolding -> normal behaviour
        if (!byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Block?.WildCardMatch("scaffolding-*-*") ?? true) return false;

        // when rmb on top-side of scaffolding -> add scaffolding to player look direction
        if (blockSel.Face == BlockFacing.UP)
        {
            TryAddScaffoldingToSide(world, byPlayer, blockSel);
            return true;
        }

        // when rmb scaffolding -> add scaffolding to top
        TryAddScaffolding(world, byPlayer, blockSel);

        return true;
    }

    private void TryAddScaffolding(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        Vec3i up = new Vec3i(0, 1, 0);
        TryAddScaffoldingToDirection(world, byPlayer, blockSel, up);
    }

    private void TryAddScaffoldingToSide(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
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

        TryAddScaffoldingToDirection(world, byPlayer, blockSel, dir);
    }

    private void TryAddScaffoldingToDirection(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Vec3i direction)
    {
        // walk in direction until there is no more scaffolding
        BlockPos current_pos = blockSel.Position.Copy();
        do
        {
            current_pos.Add(direction);
        } while (GetBlockEntity(current_pos) != null);

        // place scaffolding and remove one from the players inventory
        string failureCode = "";
        BlockSelection newBlockSelection = new(current_pos, BlockFacing.UP, this);
        var itemstack = byPlayer.WorldData.CurrentGameMode == EnumGameMode.Creative ? byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack : byPlayer.InventoryManager.ActiveHotbarSlot?.TakeOut(1);
        TryPlaceBlock(world, byPlayer, itemstack, newBlockSelection, ref failureCode);
    }

    public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
    {
        base.OnNeighbourBlockChange(world, pos, neibpos);
        if (neibpos.Y == pos.Y + 1 || neibpos.Y == pos.Y - 1)
        {
            UpdateType(pos);
        }
        // if block below is scaffolding, it means the case is already handled
        if (neibpos.Y == pos.Y - 1 && GetBlockEntity(neibpos) == null)
        {
            UpdateStability(pos);
        }
    }

    private void UpdateStability(BlockPos pos)
    {
        var entity = GetBlockEntity(pos);
        bool isChiseledBlockSolid = api.World.BlockAccessor.GetBlockEntity<BlockEntityMicroBlock>(pos.DownCopy())?.sideAlmostSolid[4] == true;
        api.Logger.Chat(isChiseledBlockSolid.ToString());
        if (entity.IsRoot && !isChiseledBlockSolid)
        {
            OnBlockBelowDestroyed(pos, entity);
        }
        else
        {
            var (maxStability, maxStabilityPos) = GetMaxStability(pos);

            if (maxStability <= 0)
            {
                api.World.BlockAccessor.BreakBlock(pos, null);
                return;
            }

            entity.Stability = maxStability;
            // block is root when it has max stability and is placed on solid ground
            bool isRoot = maxStability == MaxStability && GetBlockEntity(pos.DownCopy()) == null;
            entity.Root = isRoot ? pos : GetBlockEntity(maxStabilityPos).Root;

            // Updates surrounding stability and root
            PropogateStability(entity, true);
        }
    }

    private void OnBlockBelowDestroyed(BlockPos pos, BlockEntityScaffolding entity)
    {
        var reachable = GetReachable(pos, out var neighbours);

        foreach (var node in reachable)
        {
            node.Stability = 0;
            node.Root = null;
        }

        entity.Stability = 0;
        entity.Root = null;

        foreach (var node in neighbours)
        {
            PropogateStability(node);
        }

        if (entity.Stability <= 0)
        {
            api.World.BlockAccessor.BreakBlock(pos, null);
        }

        // check if they have a stable neighbour
        foreach (var node in reachable)
        {
            if (node.Root == null || node.Stability <= 0)
            {
                api.World.BlockAccessor.BreakBlock(node.Pos, null);
            }
        }

        foreach (var node in reachable)
        {
            if (node.Root != null && node.Stability > 0)
            {
                UpdateCode(pos);
            }
        }
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1)
    {
        if (GetBlockEntity(pos).Stability <= 0)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            return;
        }

        var reachable = GetReachable(pos, out var neighbours);

        foreach (var node in reachable)
        {
            node.Stability = 0;
            node.Root = null;
        }

        foreach (var node in neighbours)
        {
            PropogateStability(node);
        }

        // check if they have a stable neighbour
        foreach (var node in reachable)
        {
            if (node.Root == null || node.Stability < 1)
            {
                api.World.BlockAccessor.BreakBlock(node.Pos, byPlayer);
            }
        }

        foreach (var node in reachable)
        {
            if (node.Root != null && node.Stability > 0)
                UpdateCode(node.Pos);
        }

        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }

    private string GetTypeCode(BlockPos pos)
    {
        bool hasTop = api.World.BlockAccessor.GetBlockId(pos.UpCopy()) != 0;
        bool hasBot = api.World.BlockAccessor.GetBlockId(pos.DownCopy()) != 0;

        if (hasTop && hasBot) return "plain";
        else if (hasTop) return "bot";
        else if (hasBot) return "top";
        else return "tb";
    }

    private string GetOrientationCode(BlockPos pos, BlockPos maxStabilityPos, IPlayer player)
    {
        if (maxStabilityPos == null) return GetOrientationCodeFromLookDir(player);

        var dir = maxStabilityPos.SubCopy(pos);

        if (dir.Y == -1) return GetOrientationCodeFromLookDir(player);
        else if (dir.X == 1 || dir.X == -1) return "we";
        else if (dir.Z == 1 || dir.Z == -1) return "ns";
        else return "";
    }

    private string GetOrientationCodeFromLookDir(IPlayer player)
    {
        if (player == null) return LastCodePart();

        float yaw = player.Entity.Pos.Yaw * GameMath.RAD2DEG;
        int i = GameMath.Mod((int)Math.Round(yaw / 90f), 4);
        return i switch
        {
            0 => "ns",
            1 => "we",
            2 => "ns",
            3 => "we",
            _ => ""
        };
    }

    private AssetLocation GetCode(BlockPos pos, BlockPos maxStabilityPos, IPlayer player = null)
    {
        return CodeWithParts(GetTypeCode(pos), GetOrientationCode(pos, maxStabilityPos, player));
    }

    private void UpdateType(BlockPos pos)
    {
        var block = api.World.BlockAccessor.GetBlock(pos);
        Block newBlock = api.World.GetBlock(block.CodeWithPart(GetTypeCode(pos), 1));
        if (newBlock != null)
        {
            api.World.BlockAccessor.ExchangeBlock(newBlock.Id, pos);
        }
    }

    private void UpdateOrientation(BlockPos pos, IPlayer player = null)
    {
        var (_, maxStabilityPos) = GetMaxStability(pos);
        var block = api.World.BlockAccessor.GetBlock(pos);
        Block newBlock = api.World.GetBlock(block.CodeWithParts(GetOrientationCode(pos, maxStabilityPos, player)));
        if (newBlock != null)
        {
            api.World.BlockAccessor.ExchangeBlock(newBlock.Id, pos);
        }
    }

    private void UpdateCode(BlockPos pos, IPlayer player = null)
    {
        var (_, maxStabilityPos) = GetMaxStability(pos);
        var block = api.World.BlockAccessor.GetBlock(pos);
        Block newBlock = api.World.GetBlock(block.CodeWithParts(GetTypeCode(pos), GetOrientationCode(pos, maxStabilityPos, player)));
        if (newBlock != null)
        {
            api.World.BlockAccessor.ExchangeBlock(newBlock.Id, pos);
        }
    }

    public BlockEntityScaffolding GetBlockEntity(BlockPos blockPos)
    {
        return api.World.BlockAccessor.GetBlockEntity<BlockEntityScaffolding>(blockPos);
    }

    // Iterates over neighbours that have BlockEntityScaffolding
    public IEnumerable<BlockEntityScaffolding> Neighbours(BlockPos pos)
    {
        BlockEntityScaffolding be;
        be = GetBlockEntity(pos.NorthCopy());
        if (be != null) yield return be;
        be = GetBlockEntity(pos.EastCopy());
        if (be != null) yield return be;
        be = GetBlockEntity(pos.SouthCopy());
        if (be != null) yield return be;
        be = GetBlockEntity(pos.WestCopy());
        if (be != null) yield return be;
    }

    /// Searches for the most stable neighbour
    /// Calculates the resulting stability when attaching to it and returns it
    public (int max, BlockPos maxPos) GetMaxStability(BlockPos pos)
    {
        var currentEntity = GetBlockEntity(pos.DownCopy());
        int maxStability = int.MinValue;
        BlockPos maxPos = null;
        if (currentEntity != null)
        {
            maxStability = currentEntity.Stability;
            maxPos = currentEntity.Pos;
        }
        else if (
            api.World.BlockAccessor.IsSideSolid(pos.X, pos.Y - 1, pos.Z, BlockFacing.UP) ||
            api.World.BlockAccessor.GetBlockEntity<BlockEntityMicroBlock>(pos.DownCopy())?.sideAlmostSolid[4] == true)
        {
            return (MaxStability, pos.DownCopy());
        }

        foreach (var neighbour in Neighbours(pos))
        {
            if (maxStability < neighbour.Stability - 1)
            {
                maxStability = neighbour.Stability - 1;
                maxPos = neighbour.Pos;
            }
        }

        return (maxStability, maxPos?.Copy());
    }

    /// Checks all neighbours if they can increase their stability when attaching to this block
    /// And calls their PropogateStability()
    public void PropogateStability(BlockEntityScaffolding from, bool updateOrientation = false)
    {
        from.MarkDirty();
        if (updateOrientation)
        {
            var (_, maxPos) = GetMaxStability(from.Pos);
            UpdateOrientation(from.Pos);
        }

        var currentEntity = GetBlockEntity(from.Pos.UpCopy());
        if (currentEntity?.Stability < from.Stability)
        {
            currentEntity.Stability = from.Stability;
            currentEntity.Root = from.Root;
            PropogateStability(currentEntity, updateOrientation);
        }

        foreach (var neighbour in Neighbours(from.Pos))
        {
            if (neighbour.Stability < from.Stability - 1)
            {
                neighbour.Stability = from.Stability - 1;
                neighbour.Root = from.Root;
                PropogateStability(neighbour, updateOrientation);
            }
        }
    }

    // yield returns all reachable notes that life in the sub-tree (same root and decreasing stability)
    // optionally returns nodes who are bordering this sub-tree (but inside another sub-tree)
    public List<BlockEntityScaffolding> GetReachable(BlockPos pos)
    {
        var neighbours = new List<BlockEntityScaffolding>();
        return GetReachable(pos, out neighbours);
    }

    public List<BlockEntityScaffolding> GetReachable(BlockPos pos, out List<BlockEntityScaffolding> neighbours)
    {
        var queue = new Queue<BlockPos>();
        var visited = new HashSet<BlockPos>();
        var reachable = new List<BlockEntityScaffolding>();
        neighbours = new();

        if (GetBlockEntity(pos) != null)
        {
            queue.Enqueue(pos);
        }
        else
        {
            if (GetBlockEntity(pos.UpCopy()) != null)
            {
                queue.Enqueue(pos.UpCopy());
            }
            foreach (var neighbour in Neighbours(pos))
            {
                queue.Enqueue(neighbour.Pos);
            }
        }

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            var nodeEntity = GetBlockEntity(node);

            if (visited.Contains(node)) continue;
            visited.Add(node);

            if (!node.Equals(pos))
            {
                reachable.Add(nodeEntity);
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
                    neighbours.Add(currentEntity);
                }
            }

            // add side blocks to reachable and neighbour
            foreach (var neighbour in Neighbours(node))
            {
                if (visited.Contains(neighbour.Pos)) continue;

                if (neighbour.Stability < nodeEntity.Stability
                        && neighbour.Root.Equals(nodeEntity.Root))
                {
                    queue.Enqueue(neighbour.Pos);
                }
                else if (neighbour.Stability >= nodeEntity.Stability
                        || !neighbour.Root.Equals(nodeEntity.Root))
                {
                    neighbours.Add(neighbour);
                    visited.Add(neighbour.Pos);
                }
            }
        }
        return reachable;
    }
}
