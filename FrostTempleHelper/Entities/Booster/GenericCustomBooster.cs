﻿using System;
using System.Collections;
using System.Reflection;
using Celeste;
using Celeste.Mod.Meta;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;

namespace FrostHelper.Entities.Boosters
{
    [Tracked(true)]
    public class GenericCustomBooster : Entity
    {
        public bool BoostingPlayer { get; private set; }
        public string reappearSfx;
        public string enterSfx;
        public string boostSfx;
        public string endSfx;
        public float BoostTime;
        public Color ParticleColor;
        public bool Red;
        public float RespawnTime;

        public GenericCustomBooster(EntityData data, Vector2 offset) : base(data.Position + offset)
        {
            Depth = -8500;
            Collider = new Circle(10f, 0f, 2f);
            sprite = new Sprite(GFX.Game, data.Attr("directory", "objects/FrostHelper/blueBooster/"))
            {
                Visible = true
            };
            sprite.CenterOrigin();
            sprite.Justify = (new Vector2(0.5f, 0.5f));
            sprite.AddLoop("loop", "booster", 0.1f, 0, 1, 2, 3, 4);
            sprite.AddLoop("inside", "booster", 0.1f, 5, 6, 7, 8);
            sprite.AddLoop("spin", "booster", 0.06f, 18, 19, 20, 21, 22, 23, 24, 25);
            sprite.Add("pop", "booster", 0.08f, 9, 10, 11, 12, 13, 14, 15, 16, 17);
            sprite.Play("loop", false);
            Add(sprite);

            Add(new PlayerCollider(new Action<Player>(OnPlayer), null, null));
            Add(light = new VertexLight(Color.White, 1f, 16, 32));
            Add(bloom = new BloomPoint(0.1f, 16f));
            Add(wiggler = Wiggler.Create(0.5f, 4f, delegate (float f)
            {
                sprite.Scale = Vector2.One * (1f + f * 0.25f);
            }, false, false));
            Add(dashRoutine = new Coroutine(false));

            Add(dashListener = new DashListener());
            dashListener.OnDash = new Action<Vector2>(OnPlayerDashed);

            Add(new MirrorReflection());
            Add(loopingSfx = new SoundSource());
            
            particleType = (Red ? Booster.P_BurstRed : Booster.P_Burst);

            RespawnTime = data.Float("respawnTime", 1f);
            BoostTime = data.Float("boostTime", 0.3f);
            ParticleColor = ColorHelper.GetColor(data.Attr("particleColor", "Yellow"));
            reappearSfx = data.Attr("reappearSfx", "event:/game/04_cliffside/greenbooster_reappear");
            enterSfx = data.Attr("enterSfx", "event:/game/04_cliffside/greenbooster_enter");
            boostSfx = data.Attr("boostSfx", "event:/game/04_cliffside/greenbooster_dash");
            endSfx = data.Attr("releaseSfx", "event:/game/04_cliffside/greenbooster_end");

            Red = data.Bool("red", false);
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);
            Image image = new Image(GFX.Game["objects/booster/outline"]);
            image.CenterOrigin();
            image.Color = Color.White * 0.75f;
            outline = new Entity(Position)
            {
                Depth = 8999,
                Visible = false
            };
            outline.Add(image);
            outline.Add(new MirrorReflection());
            scene.Add(outline);
        }

        public void Appear()
        {
            Audio.Play(reappearSfx, Position);
            sprite.Play("appear", false, false);
            wiggler.Start();
            Visible = true;
            AppearParticles();
        }

        private void AppearParticles()
        {
            ParticleSystem particlesBG = SceneAs<Level>().ParticlesBG;
            for (int i = 0; i < 360; i += 30)
            {
                particlesBG.Emit(Red ? Booster.P_RedAppear : Booster.P_Appear, 1, Center, Vector2.One * 2f, ParticleColor, i * 0.0174532924f);
            }
        }

        public virtual void OnPlayer(Player player)
        {
            bool flag = respawnTimer <= 0f && cannotUseTimer <= 0f && !BoostingPlayer;
            if (flag)
            {
                cannotUseTimer = 0.45f;

                Boost(player);

                Audio.Play(enterSfx, Position);
                wiggler.Start();
                sprite.Play("inside", false, false);
                sprite.FlipX = (player.Facing == Facings.Left);
            }
        }

        public bool StartedBoosting;
        public virtual void Boost(Player player)
        {
            player.StateMachine.State = CustomBoostState;
            RedDash = Red;
            player.Speed = Vector2.Zero;
            //player.boostTarget = booster.Center;
            //player.boostRed = false;
            FrostModule.player_boostTarget.SetValue(player, Center);
            StartedBoosting = true;
            //player.CurrentBooster = booster;
            //LastBooster = booster;
        }
        
        public void PlayerBoosted(Player player, Vector2 direction)
        {
            if (Red)
            {
                loopingSfx.Play("event:/game/05_mirror_temple/redbooster_move", null, 0f);
                loopingSfx.DisposeOnTransition = false;
            }
            StartedBoosting = false;
            Audio.Play(boostSfx, Position);
            BoostingPlayer = true;
            Tag = (Tags.Persistent | Tags.TransitionUpdate);
            sprite.Play("spin", false, false);
            sprite.FlipX = (player.Facing == Facings.Left);
            outline.Visible = true;
            wiggler.Start();
            dashRoutine.Replace(BoostRoutine(player, direction));
        }

        private IEnumerator BoostRoutine(Player player, Vector2 dir)
        {
            float angle = (-dir).Angle();
            while ((player.StateMachine.State == Player.StDash || player.StateMachine.State == CustomRedBoostState) && BoostingPlayer)
            {
                if (player.Dead)
                {
                    PlayerDied();
                }
                else
                {
                    sprite.RenderPosition = player.Center + Booster.playerOffset;
                    loopingSfx.Position = sprite.Position;
                    bool flag = Scene.OnInterval(0.02f);
                    if (flag)
                    {
                        (Scene as Level).ParticlesBG.Emit(particleType, 2, player.Center - dir * 3f + new Vector2(0f, -2f), new Vector2(3f, 3f), ParticleColor, angle);
                    }
                    yield return null;
                }

            }
            PlayerReleased();

            if (player.StateMachine.State == CustomBoostState)
            {
                sprite.Visible = false;
            }
            while (SceneAs<Level>().Transitioning)
            {
                yield return null;
            }
            Tag = 0;
            yield break;
        }

        public virtual void OnPlayerDashed(Vector2 direction)
        {
            bool boostingPlayer = BoostingPlayer;
            if (boostingPlayer)
            {
                BoostingPlayer = false;
            }
        }

        public virtual void PlayerReleased()
        {
            Audio.Play(endSfx, sprite.RenderPosition);
            sprite.Play("pop", false, false);
            cannotUseTimer = 0f;
            respawnTimer = RespawnTime;
            BoostingPlayer = false;
            wiggler.Stop();
            loopingSfx.Stop(true);
        }

        public virtual void PlayerDied()
        {
            bool boostingPlayer = BoostingPlayer;
            if (boostingPlayer)
            {
                PlayerReleased();
                dashRoutine.Active = false;
                Tag = 0;
            }
        }

        public virtual void Respawn()
        {
            Audio.Play(reappearSfx, Position);
            sprite.Position = Vector2.Zero;
            sprite.Play("loop", true, false);
            wiggler.Start();
            sprite.Visible = true;
            outline.Visible = false;
            AppearParticles();
        }

        public override void Update()
        {
            base.Update();
            bool flag = cannotUseTimer > 0f;
            if (flag)
            {
                cannotUseTimer -= Engine.DeltaTime;
            }
            bool flag2 = respawnTimer > 0f;
            if (flag2)
            {
                respawnTimer -= Engine.DeltaTime;
                bool flag3 = respawnTimer <= 0f;
                if (flag3)
                {
                    Respawn();
                }
            }
            bool flag4 = !dashRoutine.Active && respawnTimer <= 0f;
            if (flag4)
            {
                Vector2 target = Vector2.Zero;
                Player entity = Scene.Tracker.GetEntity<Player>();
                bool flag5 = entity != null && CollideCheck(entity);
                if (flag5)
                {
                    target = entity.Center + Booster.playerOffset - Position;
                }
                sprite.Position = Calc.Approach(sprite.Position, target, 80f * Engine.DeltaTime);
            }
            bool flag6 = sprite.CurrentAnimationID == "inside" && !BoostingPlayer && !CollideCheck<Player>();
            if (flag6)
            {
                sprite.Play("loop", false, false);
            }
        }

        public override void Render()
        {
            Vector2 position = sprite.Position;
            sprite.Position = position.Floor();
            bool flag = sprite.CurrentAnimationID != "pop" && sprite.Visible;
            if (flag)
            {
                sprite.DrawOutline(1);
            }
            base.Render();
            sprite.Position = position;
        }

        // Note: this type is marked as 'beforefieldinit'.
        static GenericCustomBooster()
        {
            playerOffset = new Vector2(0f, -2f);
        }

        public virtual void HandleBoostBegin(Player player)
        {
            Level level = player.SceneAs<Level>();
            bool doNotDropTheo = false;
            if (level != null)
            {
                MapMetaModeProperties meta = level.Session.MapData.GetMeta();
                doNotDropTheo = (meta != null) && meta.TheoInBubble.GetValueOrDefault();
            }
            player.RefillDash();
            player.RefillStamina();
            if (doNotDropTheo)
            {
                return;
            }
            player.Drop();
        }

        

        public static ParticleType P_Burst => Booster.P_Burst;

        public static ParticleType P_BurstRed => Booster.P_BurstRed;

        public static ParticleType P_Appear => Booster.P_Appear;

        public static ParticleType P_RedAppear => Booster.P_RedAppear;

        public static readonly Vector2 playerOffset;

        public Sprite sprite;

        public Entity outline;

        public Wiggler wiggler;

        public BloomPoint bloom;

        public VertexLight light;

        public Coroutine dashRoutine;

        public DashListener dashListener;

        public ParticleType particleType;

        public float respawnTimer;

        public float cannotUseTimer;

        //private bool red = false;

        public SoundSource loopingSfx;

        #region RedBoostState
        public static MethodInfo Player_CallDashEvents = typeof(Player).GetMethod("CallDashEvents", BindingFlags.NonPublic | BindingFlags.Instance);
        public static MethodInfo Player_DashAssistInit = typeof(Player).GetMethod("DashAssistInit", BindingFlags.NonPublic | BindingFlags.Instance);
        public static MethodInfo Player_CorrectDashPrecision = typeof(Player).GetMethod("CorrectDashPrecision", BindingFlags.NonPublic | BindingFlags.Instance);
        public static MethodInfo Player_SuperJump = typeof(Player).GetMethod("SuperJump", BindingFlags.NonPublic | BindingFlags.Instance);
        public static MethodInfo Player_SuperWallJump = typeof(Player).GetMethod("SuperWallJump", BindingFlags.NonPublic | BindingFlags.Instance);
        public static MethodInfo Player_ClimbJump = typeof(Player).GetMethod("ClimbJump", BindingFlags.NonPublic | BindingFlags.Instance);

        public static int CustomRedBoostState;
        public static void RedDashBegin()
        {
            Player player = FrostModule.StateGetPlayer();
            DynData<Player> data = new DynData<Player>(player);
            data["calledDashEvents"] = false;
            data["dashStartedOnGround"] = false;
            Celeste.Celeste.Freeze(0.05f);
            Dust.Burst(player.Position, (-player.DashDir).Angle(), 8, null);
            data["dashCooldownTimer"] = 0.2f;
            data["dashRefillCooldownTimer"] = 0.1f;
            data["StartedDashing"] = true;
            (player.Scene as Level).Displacement.AddBurst(player.Center, 0.5f, 0f, 80f, 0.666f, Ease.QuadOut, Ease.QuadOut);
            Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
            data["dashAttackTimer"] = 0.3f;
            data["gliderBoostTimer"] = 0.55f;
            player.DashDir = (player.Speed = Vector2.Zero);
            if (!data.Get<bool>("onGround") && player.Ducking && player.CanUnDuck)
            {
                player.Ducking = false;
            }

            Player_DashAssistInit.Invoke(player, new object[] { });
        }

        public static void RedDashEnd()
        {
            Player player = FrostModule.StateGetPlayer();
            //Player_CallDashEvents.Invoke(player, new object[] { });
        }

        public static int RedDashUpdate()
        {
            Player player = FrostModule.StateGetPlayer();
            DynData<Player> data = new DynData<Player>(player);

            data["StartedDashing"] = false;
            bool ch9hub = false;//this.LastBooster != null && this.LastBooster.Ch9HubTransition;
            data["gliderBoostTimer"] = 0.05f;
            if (player.CanDash)
            {
                GenericCustomBooster booster = null;
                foreach (GenericCustomBooster b in player.Scene.Tracker.GetEntities<GenericCustomBooster>())
                {
                    if (b.BoostingPlayer)
                    {
                        booster = b;
                        break;
                    }
                }
                booster.BoostingPlayer = false;
                return player.StartDash();
            }
            if (player.DashDir.Y == 0f)
            {
                foreach (Entity entity in player.Scene.Tracker.GetEntities<JumpThru>())
                {
                    JumpThru jumpThru = (JumpThru)entity;
                    if (player.CollideCheck(jumpThru) && player.Bottom - jumpThru.Top <= 6f)
                    {
                        player.MoveVExact((int)(jumpThru.Top - player.Bottom), null, null);
                    }
                }
                if (player.CanUnDuck && Input.Jump.Pressed && data.Get<float>("jumpGraceTimer") > 0f && !ch9hub)
                {
                    //player.SuperJump();
                    Player_SuperJump.Invoke(player, null);
                    return 0;
                }
            }
            if (!ch9hub)
            {
                if (data.Get<bool>("SuperWallJumpAngleCheck"))
                {
                    if (Input.Jump.Pressed && player.CanUnDuck)
                    {
                        if ((bool)FrostModule.player_WallJumpCheck.Invoke(player, new object[] { 1 }))
                        {
                            Player_SuperWallJump.Invoke(player, new object[] { -1 });
                            return 0;
                        }
                        if ((bool)FrostModule.player_WallJumpCheck.Invoke(player, new object[] { -1 }))
                        {
                            Player_SuperWallJump.Invoke(player, new object[] { 1 });
                            return 0;
                        }
                    }
                }
                else if (Input.Jump.Pressed && player.CanUnDuck)
                {
                    if ((bool)FrostModule.player_WallJumpCheck.Invoke(player, new object[] { 1 }))
                    {
                        if (player.Facing == Facings.Right && Input.Grab.Check && player.Stamina > 0f && player.Holding == null && !ClimbBlocker.Check(player.Scene, player, player.Position + Vector2.UnitX * 3f))
                        {
                            Player_ClimbJump.Invoke(player, null);
                        }
                        else
                        {
                            FrostModule.player_WallJump.Invoke(player, new object[] { -1 });
                        }
                        return 0;
                    }
                    if ((bool)FrostModule.player_WallJumpCheck.Invoke(player, new object[] { -1 }))
                    {
                        if (player.Facing == Facings.Left && Input.Grab.Check && player.Stamina > 0f && player.Holding == null && !ClimbBlocker.Check(player.Scene, player, player.Position + Vector2.UnitX * -3f))
                        {
                            Player_ClimbJump.Invoke(player, null);
                        }
                        else
                        {
                            FrostModule.player_WallJump.Invoke(player, new object[] { 1 });
                        }
                        return 0;
                    }
                }
            }
            return CustomRedBoostState;//5;
        }

        public static IEnumerator RedDashCoroutine()
        {
            Player player = FrostModule.StateGetPlayer();
            DynData<Player> data = new DynData<Player>(player);

            yield return null;
            player.Speed = (Vector2)Player_CorrectDashPrecision.Invoke(player, new object[] { data.Get<Vector2>("lastAim") }) * 240f;
            data["gliderBoostDir"] = (player.DashDir = data.Get<Vector2>("lastAim"));
            player.SceneAs<Level>().DirectionalShake(player.DashDir, 0.2f);
            if (player.DashDir.X != 0f)
            {
                player.Facing = (Facings)Math.Sign(player.DashDir.X);
            }
            Player_CallDashEvents.Invoke(player, null);
            yield break;
        }
        #endregion

        #region BoostState

        public static int CustomBoostState;
        public static bool RedDash;

        public static void BoostBegin()
        {
            Player player = FrostModule.StateGetPlayer();
            GetBoosterThatIsBoostingPlayer(player).HandleBoostBegin(player);
            /*
            Level level = player.SceneAs<Level>();
            bool? flag;
            if (level == null)
            {
                flag = null;
            }
            else
            {
                MapMetaModeProperties meta = level.Session.MapData.GetMeta();
                flag = ((meta != null) ? meta.TheoInBubble : null);
            }
            bool? flag2 = flag;
            player.RefillDash();
            player.RefillStamina();
            if (flag2.GetValueOrDefault())
            {
                return;
            }
            player.Drop();*/
        }

        public static int BoostUpdate()
        {
            Player player = FrostModule.StateGetPlayer();
            Vector2 boostTarget = (Vector2)FrostModule.player_boostTarget.GetValue(player);
            Vector2 value = Input.Aim.Value * 3f;
            Vector2 vector = Calc.Approach(player.ExactPosition, boostTarget - player.Collider.Center + value, 80f * Engine.DeltaTime);
            player.MoveToX(vector.X, null);
            player.MoveToY(vector.Y, null);
            bool pressed = Input.Dash.Pressed || Input.CrouchDashPressed;
            // the state we should be in afterwards
            int result;
            if (pressed)
            {
                Input.Dash.ConsumePress();
                Input.CrouchDash.ConsumePress();
                result = RedDash ? CustomRedBoostState : Player.StDash;
            }
            else
            {
                result = CustomBoostState;
            }
            return result;
        }

        public static void BoostEnd()
        {
            Player player = FrostModule.StateGetPlayer();
            Vector2 boostTarget = (Vector2)FrostModule.player_boostTarget.GetValue(player);
            Vector2 vector = (boostTarget - player.Collider.Center).Floor();
            player.MoveToX(vector.X, null);
            player.MoveToY(vector.Y, null);
        }

        public static IEnumerator BoostCoroutine()
        {
            Player player = FrostModule.StateGetPlayer();
            GenericCustomBooster booster = null;
            foreach (GenericCustomBooster b in player.Scene.Tracker.GetEntities<GenericCustomBooster>())
            {
                if (b.StartedBoosting)
                {
                    booster = b;
                    break;
                }
            }
            yield return booster.BoostTime;
            player.StateMachine.State = RedDash ? CustomRedBoostState : Player.StDash;
            yield break;
        }

        protected static GenericCustomBooster GetBoosterThatIsBoostingPlayer(Player player)
        {
            GenericCustomBooster booster = null;
            foreach (GenericCustomBooster b in Engine.Scene.Tracker.GetEntities<GenericCustomBooster>())
            {
                if (b.CollideCheck(player))
                {
                    booster = b;
                    break;
                }
            }
            return booster;
        }

        #endregion
    }
}
