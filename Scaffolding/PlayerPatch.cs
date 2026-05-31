using Vintagestory.GameContent;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

using HarmonyLib;

using System;
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
        var climbMethod = AccessTools.Method(typeof(PlayerPatches), nameof(InjectCustomClimbing));
        var climbUpSpeedField = AccessTools.Field(typeof(EntityBehaviorControlledPhysics), "climbUpSpeed");
        var climbDownSpeedField = AccessTools.Field(typeof(EntityBehaviorControlledPhysics), "climbDownSpeed");
        int counter = 0;

        for (int i = 0; i < codes.Count; i++)
        {
            var code = codes[i];

            // insert logic before if (!remote) block 
            if (code.opcode == OpCodes.Ldarg_S && Convert.ToInt32(code.operand) == 4) 
            {
                // copy GOTO targets from Ldargs_S to here
                CodeInstruction instruction = new CodeInstruction(OpCodes.Ldloc_2); // BlockAccessor
                instruction.labels.AddRange(code.labels);
                code.labels.Clear();
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Ldarg_0); // instance
                yield return new CodeInstruction(OpCodes.Ldloc_0); // entity
                yield return new CodeInstruction(OpCodes.Ldarg_2); // controls
                yield return new CodeInstruction(OpCodes.Call, climbMethod);
                yield return new CodeInstruction(OpCodes.Stloc, 22); // store return value inside canClimbAnywhere
            }
            // scaling climbUpSpeed --- I know that I am checking for climbDownSpeedField, it is correct. They named it wrong
            else if (code.opcode == OpCodes.Ldfld && Equals(code.operand, climbDownSpeedField)) 
            {
                yield return code;
                yield return new CodeInstruction(OpCodes.Ldloc, 22); // isClimbingOnScaffolding aka. canClimbAnywhere
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PlayerPatches), nameof(GetClimbUpMult)));
                yield return new CodeInstruction(OpCodes.Mul);
                continue;
            }
            // scaling climbDownSpeed --- I know that I am checking for climbUpSpeedField, it is correct. They named it wrong
            else if (code.opcode == OpCodes.Ldfld && Equals(code.operand, climbUpSpeedField)) 
            {
                yield return code;
                yield return new CodeInstruction(OpCodes.Ldloc, 22); // isClimbingOnScaffolding aka. canClimbAnywhere
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PlayerPatches), nameof(GetClimbDownMult)));
                yield return new CodeInstruction(OpCodes.Mul);
                continue;
            }
            // matching sequenz for scaling horizontal climbing speed
            else if (
                code.opcode == OpCodes.Mul &&
                codes[i-1].opcode == OpCodes.Conv_R8 && 
                codes[i-2].opcode == OpCodes.Ldloc_3)
            {
                switch(counter % 3) {
                    case 1:
                        counter++;
                        break;
                    default:
                        yield return code;
                        yield return new CodeInstruction(OpCodes.Ldloc, 22); // isClimbingOnScaffolding aka. canClimbAnywhere
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PlayerPatches), nameof(GetHorizontalMult)));
                        yield return new CodeInstruction(OpCodes.Mul);
                        counter++;
                        continue;
                }
            }
            // scaling horizontal speed again bc that shit is weird
            else if (code.opcode == OpCodes.Ldarg_1 && codes[i-1].opcode == OpCodes.Ldloc_0)
            {
                yield return code;
                yield return new CodeInstruction(OpCodes.Ldloc, 22); // isClimbingOnScaffolding aka. canClimbAnywhere
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PlayerPatches), nameof(ApplyHorizontalMult)));
                continue;
            }
            yield return code;
        }
    }

    // returns if player was climbing on scaffolding
    public static bool InjectCustomClimbing(IBlockAccessor blockAccessor, object instance, Entity entity, EntityControls controls)
    {
        // only effect scaffolding climbing behavior
        if (instance is not EntityBehaviorPlayerPhysics) return false;

        // null checks for sanity
        if (entity == null || controls == null) return false;

        // prevent sliding on the ground
        if (controls.Sneak && (Math.Abs(entity.Pos.Motion.Y) <= 0.01f || entity.OnGround)) return false; 

        foreach (var (blockPos, facing, _) in IterateBlocksInRange(entity.Pos, 0.5f)) 
        {
            Block block = blockAccessor.GetBlock(blockPos);
            if (!block?.WildCardMatch("scaffolding-*-*") ?? true) continue;

            controls.IsClimbing = true;
            entity.ClimbingIntoFace = facing;
            return true;
        }

        return false;
    }

    private static List<(BlockPos, BlockFacing, double)> IterateBlocksInRange(EntityPos pos, float r) 
    {
        int min_x = (int)Math.Floor(pos.X - r);
        int max_x = (int)Math.Floor(pos.X + r);
        int min_z = (int)Math.Floor(pos.Z - r);
        int max_z = (int)Math.Floor(pos.Z + r);

        List<(BlockPos pos, BlockFacing facing, double dd)> blocks = new();
        for (int z = min_z; z <= max_z; z++) 
        {
            for (int x = min_x; x <= max_x; x++)
            {
                double dd = GetDistanceFromBlock(x, z, pos);
                if (dd == 0)
                {
                    blocks.Add((new BlockPos(x, pos.AsBlockPos.Y, z), null, dd));
                    blocks.Add((new BlockPos(x, pos.AsBlockPos.Y + 1, z), null, dd));
                }
                else if (dd <= r*r) 
                {
                    BlockFacing facing = BlockFacing.FromVector(pos.AsBlockPos.X - x, 0, pos.AsBlockPos.Z - z);
                    blocks.Add((new BlockPos(x, pos.AsBlockPos.Y, z), facing, dd));
                    blocks.Add((new BlockPos(x, pos.AsBlockPos.Y + 1, z), facing, dd));
                }
            }
        }

        // sort blocks ascending w.r.t. squared distance
        blocks.Sort((a,b) => a.dd.CompareTo(b.dd));
        return blocks;
    }

    private static double GetDistanceFromBlock(int x, int z, EntityPos playerPos)
    {
        if (x == playerPos.AsBlockPos.X && z == playerPos.AsBlockPos.Z) return 0;

        double closest_x = Math.Max((double)x, Math.Min(playerPos.X, (double)x + 1.0));
        double closest_z = Math.Max((double)z, Math.Min(playerPos.Z, (double)z + 1.0));

        return playerPos.SquareDistanceTo(closest_x, playerPos.Y, closest_z);
    }

    private static double GetClimbUpMult(bool isClimbingOnScaffolding) => isClimbingOnScaffolding? ModConfig.Data.ClimbUpSpeedMult : 1.0;
    private static double GetClimbDownMult(bool isClimbingOnScaffolding) => isClimbingOnScaffolding? ModConfig.Data.ClimbDownSpeedMult : 1.0;
    private static double GetHorizontalMult(bool isClimbingOnScaffolding) => isClimbingOnScaffolding? ModConfig.Data.HorizontalSpeedMult : 1.0;

    private static EntityPos ApplyHorizontalMult(EntityPos pos, bool isClimbingOnScaffolding) 
    {
        if (!isClimbingOnScaffolding) return pos;
        EntityPos copy = pos.Copy();
        copy.Motion.X *= GetHorizontalMult(true);
        copy.Motion.Z *= GetHorizontalMult(true);
        return copy;
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
