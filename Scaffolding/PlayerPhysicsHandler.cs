using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Common.Entities;
using Vintagestory.GameContent;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using System;

namespace Scaffolding
{
    public class PlayerPhysicsHandler : EntityBehavior, IPhysicsTickable
    {
        public bool Ticking { get; set; } = true;
        private EntityPlayer Player;
        public Entity Entity => Player;

        private const float drag = 1.5f;
        private const float okSpeed = 0.1f;

        private ICoreAPI Api => Player.Api;

        public PlayerPhysicsHandler(EntityPlayer player, ICoreServerAPI api = null) : base(player)
        {
            Player = player;
            api?.Server.AddPhysicsTickable(this);
        }

        public override string PropertyName()
        {
            return "scaffolding:climb";
        }

        public void OnPhysicsTick(float dt)
        {
            Api.Logger.Chat((Player.Pos.Motion.Y / dt).ToString());
            Player.Pos.Motion.Y = 0;
            Player.ServerPos.Motion.Y = 0;
        }

        public void AfterPhysicsTick(float dt) { }

        public override void OnGameTick(float dt)
        {
            var blockAccessor = Api.World.BlockAccessor;
            BlockPos playerBlockPos = Player.Pos.AsBlockPos;

            if (blockAccessor.GetBlock(playerBlockPos)?.Code?.Path == "scaffolding"
             || blockAccessor.GetBlock(playerBlockPos.UpCopy()).Code?.Path == "scaffolding")
            {
                // player is inside a scaffolding
                Player.Pos.Motion.Y = 0;
                Player.ServerPos.Motion.Y = 0;
            }
            else if (blockAccessor.GetBlock(playerBlockPos.DownCopy())?.Code?.Path == "scaffolding"
                    && Player.Pos.Motion.Y > -0.1f)
            {
                // player is not inside but ontop of a scaffolding
                HandleWalk();
            }
        }

        private void HandleClimb(float dt)
        {
            var controls = Player.Controls;
            var vy = Player.Pos.Motion.Y;

            if (vy < -okSpeed && !controls.Sneak)
            {
                vy += Math.Min(drag * dt, -vy);
            }
            else if (vy >= -okSpeed && controls.Jump)
            {
                vy = 0.05f;
            }
            else if (vy >= -okSpeed && controls.Sneak)
            {
                vy = -0.05f;
            }
            else if (vy >= -okSpeed && !controls.Sneak && !controls.Jump)
            {
            }
        }

        private void HandleWalk() { }
    }
}
