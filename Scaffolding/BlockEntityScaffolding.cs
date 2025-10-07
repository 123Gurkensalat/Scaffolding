using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;


namespace Scaffolding.BlockEntities;
public class BlockEntityScaffolding : BlockEntity
{
    public int Stability { get; set; }
    public BlockPos Root = null;
    public bool IsRoot => Root.Equals(Pos);

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
