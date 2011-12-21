﻿/*
 * Copyright (C) 2011 mooege project
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */

using System;
using System.Linq;
using System.Collections.Generic;
using Mooege.Core.GS.Actors;
using Mooege.Core.GS.Common.Types.Math;
using Mooege.Net.GS.Message.Definitions.ACD;
using Mooege.Core.GS.Common.Types.Misc;
using Mooege.Core.GS.Ticker;
using Mooege.Net.GS.Message;
using Mooege.Core.GS.Common.Types.TagMap;
using Mooege.Core.GS.Powers.Payloads;

namespace Mooege.Core.GS.Powers.Implementations
{
    [ImplementsPowerSNO(Skills.Skills.Barbarian.FuryGenerators.Bash)]
    public class BarbarianBash : Skill
    {
        public override IEnumerable<TickTimer> Main()
        {
            AttackPayload attack = new AttackPayload(this);
            attack.Targets = GetBestMeleeEnemy();
            attack.AddWeaponDamage(1.45f, DamageType.Physical);
            attack.OnHit = hitPayload =>
            {
                GeneratePrimaryResource(6f);

                if (Rand.NextDouble() < 0.20)
                    Knockback(hitPayload.Target, ScriptFormula(5), ScriptFormula(6), ScriptFormula(7));
            };

            attack.Apply();

            yield break;
        }

        public override float GetContactDelay()
        {
            // seems to need this custom speed for all attacks
            return ScriptFormula(13);
        }
    }

    [ImplementsPowerSNO(Skills.Skills.Barbarian.FuryGenerators.LeapAttack)]
    public class BarbarianLeap : Skill
    {
        public override IEnumerable<TickTimer> Main()
        {
            //StartCooldown(WaitSeconds(10f));

            ActorMover mover = new ActorMover(User);
            mover.MoveArc(TargetPosition, 10, -0.1f, new ACDTranslateArcMessage
            {
                //Field3 = 303110, // used for male barb leap, not needed?
                FlyingAnimationTagID = AnimationSetKeys.Attack2.ID,
                LandingAnimationTagID = -1,
                Field7 = PowerSNO
            });

            // wait for landing
            while (!mover.Update())
                yield return WaitTicks(1);

            // extra wait for leap to finish
            yield return WaitTicks(1);

            // ground smash effect
            User.PlayEffectGroup(162811);

            bool hitAnything = false;
            AttackPayload attack = new AttackPayload(this);
            attack.Targets = GetEnemiesInRadius(TargetPosition, 8f);
            attack.AddWeaponDamage(0.70f, DamageType.Physical);
            attack.OnHit = hitPayload => { hitAnything = true; };
            attack.Apply();

            if (hitAnything)
                GeneratePrimaryResource(15f);

            yield break;
        }
    }

    [ImplementsPowerSNO(Skills.Skills.Barbarian.FurySpenders.Whirlwind)]
    public class BarbarianWhirlwind : Skill
    {
        public override IEnumerable<TickTimer> Main()
        {
            AddBuff(User, new WhirlwindEffect());
            yield break;
        }

        [ImplementsPowerBuff(0)]
        public class WhirlwindEffect : PowerBuff
        {
            private TickTimer _damageTimer;
            private TickTimer _tornadoSpawnTimer;

            public override void Init()
            {
                Timeout = WaitSeconds(0.20f);
            }

            public override bool Update()
            {
                if (base.Update())
                    return true;

                if (_damageTimer == null || _damageTimer.TimedOut)
                {
                    _damageTimer = WaitSeconds(ScriptFormula(0));
                    //UsePrimaryResource(EvalTag(PowerKeys.ResourceCost));

                    WeaponDamage(GetEnemiesInRadius(User.Position, ScriptFormula(2)),
                                 ScriptFormula(1), Rune_A > 0 ? DamageType.Fire : DamageType.Physical);
                }

                if (Rune_B > 0)
                {
                    // spawn tornado projectiles in random directions every timed period
                    if (_tornadoSpawnTimer == null)
                        _tornadoSpawnTimer = WaitSeconds(ScriptFormula(5));

                    if (_tornadoSpawnTimer.TimedOut)
                    {
                        _tornadoSpawnTimer = WaitSeconds(ScriptFormula(5));

                        var tornado = new Projectile(this, 162386, User.Position);
                        tornado.Timeout = WaitSeconds(3f);
                        tornado.OnCollision = (hit) =>
                        {
                            WeaponDamage(hit, ScriptFormula(6), DamageType.Physical);
                        };
                        tornado.Launch(new Vector3D(User.Position.X + (float)Rand.NextDouble() - 0.5f,
                                                    User.Position.Y + (float)Rand.NextDouble() - 0.5f,
                                                    User.Position.Z), 0.25f);
                    }
                }

                return false;
            }
        }
    }

    [ImplementsPowerSNO(Skills.Skills.Barbarian.FuryGenerators.AncientSpear)]
    public class BarbarianAncientSpear : Skill
    {
        public override IEnumerable<TickTimer> Main()
        {
            //StartCooldown(WaitSeconds(10f));

            var projectile = new Projectile(this, 74636, User.Position);
            projectile.Timeout = WaitSeconds(0.5f);
            projectile.OnCollision = (hit) =>
            {
                GeneratePrimaryResource(15f);
                
                _setupReturnProjectile(hit.Position);

                AttackPayload attack = new AttackPayload(this);
                attack.SetSingleTarget(hit);
                attack.AddWeaponDamage(1.00f, DamageType.Physical);
                attack.OnHit = (hitPayload) =>
                {
                    // GET OVER HERE
                    Knockback(hitPayload.Target, -25f, 1f, -0.03f);
                };
                attack.Apply();

                projectile.Destroy();
            };
            projectile.OnTimeout = () =>
            {
                _setupReturnProjectile(projectile.Position);
            };

            projectile.Launch(TargetPosition, 1.9f);
            User.AddRopeEffect(79402, projectile);

            yield break;
        }

        private void _setupReturnProjectile(Vector3D spawnPosition)
        {
            Vector3D inFrontOfUser = PowerMath.TranslateDirection2D(User.Position, spawnPosition, User.Position, 5f);

            var return_proj = new Projectile(this, 79400, new Vector3D(spawnPosition.X, spawnPosition.Y, User.Position.Z));
            return_proj.DestroyOnArrival = true;
            return_proj.LaunchArc(inFrontOfUser, 1f, -0.03f);
            User.AddRopeEffect(79402, return_proj);
        }
    }
}
