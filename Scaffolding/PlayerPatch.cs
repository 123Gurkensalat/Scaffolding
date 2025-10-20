using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

using HarmonyLib;

using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;

namespace Scaffolding.Patches;

public static class PlayerPatches
{
    public static ICoreAPI Api;
    public static void ApplyAll(Harmony harmony)
    {
        Apply(harmony, typeof(EntityBehaviorControlledPhysics), "ApplyTests", transpiler: nameof(ClimbingTranspiler));
        Apply(harmony, typeof(CollisionTester), "ApplyTerrainCollision", transpiler: nameof(WalkingTranspiler));
    }

    private static void Apply(Harmony harmony, System.Type target, string function, string prefix = null, string postfix = null, string transpiler = null)
    {
        MethodInfo h_target = AccessTools.Method(target, function);

        MethodInfo h_prefix = prefix != null ? AccessTools.Method(typeof(PlayerPatches), prefix) : null;
        MethodInfo h_postfix = postfix != null ? AccessTools.Method(typeof(PlayerPatches), postfix) : null;
        MethodInfo h_transpiler = transpiler != null ? AccessTools.Method(typeof(PlayerPatches), transpiler) : null;

        harmony.Patch(h_target,
            prefix: h_prefix != null ? new HarmonyMethod(h_prefix) : null,
            postfix: h_postfix != null ? new HarmonyMethod(h_postfix) : null,
            transpiler: h_transpiler != null ? new HarmonyMethod(h_transpiler) : null);
    }

    private static IEnumerable<CodeInstruction> ClimbingTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var getCollisionBoxes = AccessTools.Method(typeof(Block), nameof(Block.GetCollisionBoxes), new[] { typeof(IBlockAccessor), typeof(BlockPos) });
        var getBlock = AccessTools.Method(typeof(IBlockAccessor), nameof(IBlockAccessor.GetBlock), new[] { typeof(BlockPos), typeof(int) });
        var injectMethod = AccessTools.Method(typeof(PlayerPatches), nameof(InjectCustomCollisionBoxes));
        var counter = 0; // used to tell at which GetCollisionBox call we are

        for (int i = 0; i < codes.Count; i++)
        {
            var code = codes[i];
            yield return code;

            if (counter == 1 && (code.opcode == OpCodes.Callvirt || code.opcode == OpCodes.Call) && code.operand is MethodInfo mj && mj == getBlock)
            {
                yield return new CodeInstruction(OpCodes.Stloc, 26);
                yield return new CodeInstruction(OpCodes.Ldloc, 26);
            }

            if (code.opcode == OpCodes.Callvirt && code.operand is MethodInfo mi && mi == getCollisionBoxes)
            {
                switch (counter)
                {
                    case 0:
                        yield return new CodeInstruction(OpCodes.Ldloc, 26);
                        break;
                    case 1:
                        yield return new CodeInstruction(OpCodes.Ldloc, 26);
                        break;
                    case 2:
                        yield return new CodeInstruction(OpCodes.Ldloc, 36);
                        break;
                    default:
                        break;
                }
                yield return new CodeInstruction(OpCodes.Ldloc_2);
                yield return new CodeInstruction(OpCodes.Ldloc, 4);
                yield return new CodeInstruction(OpCodes.Ldarg_0);

                // Call the helper method: InjectCustomCollisionBoxes(Cuboidf[], Block, IBlockAccessor, BlockPos)
                yield return new CodeInstruction(OpCodes.Call, injectMethod);
                counter++;
            }
        }
    }

    /// <summary>
    /// Receives the original collision boxes and the block instance
    /// </summary>
    public static Cuboidf[] InjectCustomCollisionBoxes(Cuboidf[] original, Block block, IBlockAccessor accessor, BlockPos pos, object instance)
    {
        if (instance is not EntityBehaviorPlayerPhysics) return original;

        if (block == null || accessor == null || pos == null) return original;

        if (block.WildCardMatch("scaffolding-*-*"))
        {
            var merged = new List<Cuboidf>(original ?? new Cuboidf[0]);
            merged.AddRange(block.GetSelectionBoxes(accessor, pos));
            return merged.ToArray();
        }

        return original;
    }

    private static IEnumerable<CodeInstruction> WalkingTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var generateCollisionBoxList = AccessTools.Method(typeof(CollisionTester), "GenerateCollisionBoxList");
        var injectMethod = AccessTools.Method(typeof(PlayerPatches), nameof(InjectCustomTerrainCollisionBoxes));

        for (int i = 0; i < codes.Count; i++)
        {
            var code = codes[i];
            yield return code;

            // Look for the first callvirt to GetCollisionBoxes
            if (code.opcode == OpCodes.Callvirt && code.operand is MethodInfo mi && mi == generateCollisionBoxList)
            {
                yield return new CodeInstruction(OpCodes.Ldloc_0); // WorldAccessor
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(IWorldAccessor), "get_BlockAccessor")); // blockAccessor
                yield return new CodeInstruction(OpCodes.Ldarg_0); // CollisionTester
                yield return new CodeInstruction(OpCodes.Ldarg_1); // entity

                yield return new CodeInstruction(OpCodes.Call, injectMethod);
            }
        }
    }

    /// <summary>
    /// Receives the original collision boxes and the block instance
    /// </summary>
    public static void InjectCustomTerrainCollisionBoxes(IBlockAccessor blockAccessor, CollisionTester tester, Entity entity)
    {
        if (entity is EntityPlayer ec)
        {
            if (ec.Controls.IsClimbing) return;
            if (ec.Controls.Sneak) return;

            blockAccessor.WalkBlocks(tester.minPos, tester.maxPos, (block, x, y, z) =>
                {
                    if (block?.WildCardMatch("scaffolding-*-*") == true)
                    {
                        tester.CollisionBoxList.Add(block.SelectionBoxes, x, y, z, block);
                    }
                }, true);
        }
        else if (entity is EntityItem)
        {
            blockAccessor.WalkBlocks(tester.minPos, tester.maxPos, (block, x, y, z) =>
                {
                    if (block?.WildCardMatch("scaffolding-*-*") == true)
                    {
                        tester.CollisionBoxList.Add(block.SelectionBoxes, x, y, z, block);
                    }
                }, true);
        }
    }
}
