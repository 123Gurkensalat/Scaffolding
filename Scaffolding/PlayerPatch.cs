using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Datastructures;

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

    private static bool IsLdloc(CodeInstruction instr)
    {
        return instr.opcode == OpCodes.Ldloc || instr.opcode == OpCodes.Ldloc_S ||
               instr.opcode == OpCodes.Ldloc_0 || instr.opcode == OpCodes.Ldloc_1 ||
               instr.opcode == OpCodes.Ldloc_2 || instr.opcode == OpCodes.Ldloc_3;
    }

    // Helper to convert ldloc opcode to index
    private static int GetLdlocIndex(CodeInstruction instr)
    {
        if (instr.opcode == OpCodes.Ldloc_0) return 0;
        if (instr.opcode == OpCodes.Ldloc_1) return 1;
        if (instr.opcode == OpCodes.Ldloc_2) return 2;
        if (instr.opcode == OpCodes.Ldloc_3) return 3;
        if (instr.opcode == OpCodes.Ldloc || instr.opcode == OpCodes.Ldloc_S)
        {
            if (instr.operand == null)
                throw new System.InvalidOperationException("ldloc or ldloc.s has null operand!");
            if (instr.operand is LocalBuilder lb)
                return lb.LocalIndex;

            // Sometimes the operand might be an int (rare), handle that too
            if (instr.operand is int i)
                return i;

            throw new System.InvalidOperationException($"Unsupported ldloc operand type: {instr.operand.GetType()}");
        }

        throw new System.InvalidOperationException("Unexpected ldloc opcode");
    }

    private static IEnumerable<CodeInstruction> ClimbingTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var getCollisionBoxes = AccessTools.Method(typeof(Block), nameof(Block.GetCollisionBoxes),
            new[] { typeof(IBlockAccessor), typeof(BlockPos) });
        var injectMethod = AccessTools.Method(typeof(PlayerPatches), nameof(InjectCustomCollisionBoxes));

        for (int i = 0; i < codes.Count; i++)
        {
            var code = codes[i];
            yield return code;

            // Look for the first callvirt to GetCollisionBoxes
            if (code.opcode == OpCodes.Callvirt && code.operand is MethodInfo mi && mi == getCollisionBoxes)
            {
                var locals = new List<int>();
                for (int j = i - 1; j >= 0; j--)
                {
                    var c = codes[j];
                    if (IsLdloc(c))
                    {
                        locals.Add(GetLdlocIndex(c));
                        if (locals.Count == 3) break;
                    }
                }

                locals.Reverse();

                int blockIndex = locals[0];
                int accessorIndex = locals[1];
                int posIndex = locals[2];

                // Push the locals onto the stack in order: block, accessor, pos
                yield return new CodeInstruction(OpCodes.Ldloc, blockIndex);
                yield return new CodeInstruction(OpCodes.Ldloc, accessorIndex);
                yield return new CodeInstruction(OpCodes.Ldloc, posIndex);
                yield return new CodeInstruction(OpCodes.Ldarg_0);

                // Call the helper method: InjectCustomCollisionBoxes(Cuboidf[], Block, IBlockAccessor, BlockPos)
                yield return new CodeInstruction(OpCodes.Call, injectMethod);
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

        if (block.Code?.Equals("scaffolding:scaffolding") ?? false)
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
        if (tester.CollisionBoxList.Count == 0) return;
        if (entity is not EntityPlayer ec) return;
        if (ec.Controls.IsClimbing) return;
        if (ec.Controls.Sneak) return;
        //if (ec.Pos.Motion.Y > 2) return;

        blockAccessor.WalkBlocks(tester.minPos, tester.maxPos, (block, x, y, z) =>
        {
            if (block?.Code.Equals("scaffolding:scaffolding") == true)
            {
                Api.Logger.Chat("hit");
                tester.CollisionBoxList.Add(block.SelectionBoxes, x, y, z, block);
            }
        }, true);
    }

}
