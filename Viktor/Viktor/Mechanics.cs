﻿// ReSharper disable ConditionIsAlwaysTrueOrFalse
// ReSharper disable InvertIf
// ReSharper disable FunctionComplexityOverflow
// ReSharper disable ConvertClosureToMethodGroup

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace Viktor
{
    internal static class Mechanics
    {
        private static readonly Dictionary<SpellSlot, Spell> Spell = Spells.Spell;
        private static readonly Obj_AI_Hero Player = ObjectManager.Player;
        private static readonly bool PacketCast = Config.ViktorConfig.Item("apollo.viktor.packetcast").GetValue<bool>();
        public static GameObject ChaosStorm;
        public static readonly SpellSlot IgniteSlot = Player.GetSpellSlot("SummonerDot");

        public static void Init()
        {
            Game.OnUpdate += OnUpdate;
            GameObject.OnCreate += OnCreate;
            GameObject.OnDelete += OnDelete;
            Interrupter2.OnInterruptableTarget += OnInterruptableTarget;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
        }

        private static void OnUpdate(EventArgs args)
        {
            if (args == null || Player.IsDead || Player.IsRecalling())
                return;

            AutoFollowR();
            KillSteal();

            if (Config.ViktorConfig.Item("apollo.viktor.harass.key").GetValue<KeyBind>().Active)
                Harass();

            switch (Config.Orbwalker.ActiveMode)
            {
                case Orbwalking.OrbwalkingMode.Combo:
                {
                    Combo();
                    break;
                }
                case Orbwalking.OrbwalkingMode.LaneClear:
                {
                    Laneclear();
                    Jungleclear();
                    break;
                }
                case Orbwalking.OrbwalkingMode.Mixed:
                {
                    Harass();
                    break;
                }
            }
        }

        private static void Combo()
        {
            var t = TargetSelector.GetTarget(
                Spell[SpellSlot.E].Range + Spells.ECastRange, TargetSelector.DamageType.Magical);
            var useQ = Config.ViktorConfig.Item("apollo.viktor.combo.q.bool").GetValue<bool>();
            var useW = Config.ViktorConfig.Item("apollo.viktor.combo.w.bool").GetValue<bool>();
            var useE = Config.ViktorConfig.Item("apollo.viktor.combo.e.bool").GetValue<bool>();
            var useR = Config.ViktorConfig.Item("apollo.viktor.combo.r.bool").GetValue<bool>();
            var preE =
                (HitChance)
                    (Config.ViktorConfig.Item("apollo.viktor.combo.e.pre").GetValue<StringList>().SelectedIndex + 3);

            if (t == null)
                return;

            if (IgniteSlot != SpellSlot.Unknown && IgniteSlot.IsReady() &&
                Config.ViktorConfig.Item("apollo.viktor.combo.ignite.bool").GetValue<bool>() &&
                t.Health < Damages.ComboDmg(t))
            {
                Player.Spellbook.CastSpell(IgniteSlot, t);
            }

            if (useQ)
            {
                CastQ(t);
            }
            if (useW)
            {
                CastW();
            }
            if (useE)
            {
                CastE(t, preE);
            }
            if (useR)
            {
                CastR(t);
            }
        }

        private static void Harass()
        {
            var t = TargetSelector.GetTarget(1025, TargetSelector.DamageType.Magical);
            var mana = Config.ViktorConfig.Item("apollo.viktor.harass.mana").GetValue<Slider>().Value;
            var useQ = Config.ViktorConfig.Item("apollo.viktor.harass.q.bool").GetValue<bool>();
            var useE = Config.ViktorConfig.Item("apollo.viktor.harass.e.bool").GetValue<bool>();
            var preE =
                (HitChance)
                    (Config.ViktorConfig.Item("apollo.viktor.harass.e.pre").GetValue<StringList>().SelectedIndex + 3);

            if (mana > Player.ManaPercent)
            {
                return;
            }

            if (useQ)
            {
                CastQ(t);
            }
            if (useE)
            {
                CastE(t, preE);
            }
        }

        private static void Laneclear()
        {
            var mana = Player.ManaPercent > Config.ViktorConfig.Item("apollo.viktor.laneclear.mana").GetValue<Slider>().Value;
            var useQ = Config.ViktorConfig.Item("apollo.viktor.laneclear.q.bool").GetValue<bool>();
            var useE = Config.ViktorConfig.Item("apollo.viktor.laneclear.e.bool").GetValue<bool>();
            var lastHitQ = Config.ViktorConfig.Item("apollo.viktor.laneclear.q.lasthit").GetValue<bool>();
            var lastHitCanonQ = Config.ViktorConfig.Item("apollo.viktor.laneclear.q.canon").GetValue<bool>();
            var minhitE = Config.ViktorConfig.Item("apollo.viktor.laneclear.e.hit").GetValue<Slider>().Value;

            if (!mana)
                return;

            if (useQ)
            {
                var minions = MinionManager.GetMinions(Player.ServerPosition, Spell[SpellSlot.Q].Range);

                if (minions == null)
                {
                    return;
                }

                var minionLasthit =
                    minions.Where(
                        h =>
                            HealthPrediction.GetHealthPrediction(
                                h, (int)(Player.Distance(h) / Spell[SpellSlot.Q].Speed),
                                (int) (Spell[SpellSlot.Q].Delay * 1000 + Game.Ping / 2f)) < Damages.Dmg.Q(h) &&
                            HealthPrediction.GetHealthPrediction(
                                h, (int)(Player.Distance(h) / Spell[SpellSlot.Q].Speed),
                                (int) (Spell[SpellSlot.Q].Delay * 1000 + Game.Ping / 2f)) > 0)
                        .OrderBy(h => h.Health)
                        .FirstOrDefault();
                if (lastHitQ && minionLasthit != null)
                {
                    Spell[SpellSlot.Q].CastOnUnit(minionLasthit, PacketCast);
                }

                var canonLasthit =
                    minions.Where(
                        h =>
                            h.BaseSkinName.Contains("Siege") &&
                            HealthPrediction.GetHealthPrediction(
                                h, (int)(Player.Distance(h) / Spell[SpellSlot.Q].Speed),
                                (int) (Spell[SpellSlot.Q].Delay * 1000 + Game.Ping / 2f)) < Damages.Dmg.Q(h) &&
                            HealthPrediction.GetHealthPrediction(
                                h, (int)(Player.Distance(h) / Spell[SpellSlot.Q].Speed),
                                (int) (Spell[SpellSlot.Q].Delay * 1000 + Game.Ping / 2f)) > 0)
                        .OrderBy(h => h.Health)
                        .FirstOrDefault();
                if (lastHitCanonQ && canonLasthit != null)
                {
                    Spell[SpellSlot.Q].CastOnUnit(canonLasthit, PacketCast);
                }
            }
            if (useE && Spell[SpellSlot.E].IsReady())
            {
                foreach (var minion in MinionManager.GetMinions(Player.Position, Spells.ECastRange))
                {
                    var farmLocation =
                        MinionManager.GetBestLineFarmLocation(
                            MinionManager.GetMinionsPredictedPositions(
                                MinionManager.GetMinions(minion.Position, Spell[SpellSlot.E].Range),
                                Spell[SpellSlot.E].Delay, Spell[SpellSlot.E].Width, Spell[SpellSlot.E].Speed,
                                minion.Position, Spell[SpellSlot.E].Range, false, SkillshotType.SkillshotLine),
                            Spell[SpellSlot.E].Width, Spell[SpellSlot.E].Range);

                    if (farmLocation.MinionsHit >= minhitE)
                    {
                        Spell[SpellSlot.E].Cast(minion.Position.To2D(), farmLocation.Position);
                    }
                }
                
            }
        }

        private static void Jungleclear()
        {
            var minions = MinionManager.GetMinions(
                Player.ServerPosition, Spell[SpellSlot.E].Range + Spells.ECastRange, MinionTypes.All, MinionTeam.Neutral,
                MinionOrderTypes.MaxHealth);
            var mana = Player.ManaPercentage() > Config.ViktorConfig.Item("apollo.viktor.laneclear.mana").GetValue<Slider>().Value;

            if (minions == null || !mana)
                return;

            var useQ = Config.ViktorConfig.Item("apollo.viktor.laneclear.q.bool").GetValue<bool>();
            var useE = Config.ViktorConfig.Item("apollo.viktor.laneclear.e.bool").GetValue<bool>();
            var minionsQ = minions.Where(h => h.IsValidTarget(Spell[SpellSlot.Q].Range)).OrderBy(h => h.MaxHealth).FirstOrDefault();

            if (useQ && Spell[SpellSlot.Q].IsReady() && minionsQ != null)
            {
                Spell[SpellSlot.Q].CastOnUnit(minionsQ, PacketCast);
            }

            if (useE && Spell[SpellSlot.E].IsReady())
            {
                var minionE = minions.FirstOrDefault();
                if (minionE != null)
                {
                    if (Player.Distance(minionE) < Spells.ECastRange)
                    {
                        var sourcePosition = minionE.ServerPosition;
                        Spell[SpellSlot.E].UpdateSourcePosition(sourcePosition, sourcePosition);
                        var lineFarm =
                            MinionManager.GetBestLineFarmLocation(
                                minions.Select(m => m.ServerPosition.To2D()).ToList(), Spell[SpellSlot.E].Width,
                                Spell[SpellSlot.E].Range);
                        Spell[SpellSlot.E].Cast(sourcePosition, lineFarm.Position.To3D());
                    }
                    else if (Player.Distance(minionE) < Spells.ECastRange + Spell[SpellSlot.E].Range)
                    {
                        var sourcePosition = Player.ServerPosition.Extend(minionE.ServerPosition, Spells.ECastRange);
                        Spell[SpellSlot.E].UpdateSourcePosition(sourcePosition, sourcePosition);
                        var lineFarm =
                            MinionManager.GetBestLineFarmLocation(
                                minions.Select(m => m.ServerPosition.To2D()).ToList(), Spell[SpellSlot.E].Width,
                                Spell[SpellSlot.E].Range);
                        Spell[SpellSlot.E].Cast(sourcePosition, lineFarm.Position.To3D());
                    }
                }
            }
        }

        private static void CastQ(Obj_AI_Base t)
        {
            if (!Spell[SpellSlot.Q].IsReady() || t == null || !Spell[SpellSlot.Q].IsInRange(t))
            {
                return;
            }
            if (Orbwalking.InAutoAttackRange(t))
            {
                Spell[SpellSlot.Q].CastOnUnit(t, PacketCast);
            }
            else if (!Config.ViktorConfig.Item("apollo.viktor.combo.q.dont").GetValue<bool>())
            {
                Spell[SpellSlot.Q].CastOnUnit(t, PacketCast);
            }
        }

        private static void CastW()
        {
            if (!Spell[SpellSlot.W].IsReady())
            {
                return;
            }

            var stunT =
                HeroManager.Enemies.Where(
                    h =>
                        h.IsValidTarget(Spell[SpellSlot.W].Range) &&
                        (h.HasBuffOfType(BuffType.Knockup) || h.HasBuffOfType(BuffType.Snare) ||
                         h.HasBuffOfType(BuffType.Stun) || h.HasBuffOfType(BuffType.Suppression) ||
                         h.HasBuffOfType(BuffType.Taunt)) && !h.IsInvulnerable)
                    .OrderBy(h => TargetSelector.GetPriority(h))
                    .FirstOrDefault();
            if (stunT != null && Config.ViktorConfig.Item("apollo.viktor.combo.w.stunned").GetValue<bool>())
            {
                Spell[SpellSlot.W].Cast(stunT, PacketCast, true);
            }

            var slowT =
                HeroManager.Enemies.Where(
                    h =>
                        h.IsValidTarget(Spell[SpellSlot.W].Range - Spell[SpellSlot.W].Width) &&
                        h.HasBuffOfType(BuffType.Slow)).OrderBy(h => TargetSelector.GetPriority(h)).FirstOrDefault();
            var slowTpre = Spell[SpellSlot.W].GetPrediction(slowT, true, -Spell[SpellSlot.E].Width);
            if (slowT != null && Config.ViktorConfig.Item("apollo.viktor.combo.w.slow").GetValue<bool>())
            {
                Spell[SpellSlot.W].Cast(slowTpre.CastPosition, PacketCast);
            }

            var t =
                HeroManager.Enemies.Where(h => h.IsValidTarget(300))
                    .OrderBy(h => TargetSelector.GetPriority(h))
                    .FirstOrDefault();
            var tpre = Spell[SpellSlot.E].GetPrediction(t, true);
            if (t != null)
            {
                if (tpre.Hitchance >= HitChance.High)
                {
                    Spell[SpellSlot.W].Cast(tpre.CastPosition, PacketCast);
                }
                if (tpre.AoeTargetsHitCount >=
                    Config.ViktorConfig.Item("apollo.viktor.combo.w.hit").GetValue<Slider>().Value)
                {
                    Spell[SpellSlot.W].Cast(tpre.CastPosition, PacketCast);
                }
            }
        }

        private static void CastE(Obj_AI_Base t, HitChance hit)
        {
            if (t == null)
                return;
            

            if (Player.Distance(t.ServerPosition) < Spells.ECastRange && Spell[SpellSlot.E].IsReady())
            {
                var sourcePosition = t.ServerPosition;
                Spell[SpellSlot.E].UpdateSourcePosition(sourcePosition, sourcePosition);
                var preE = Spell[SpellSlot.E].GetPrediction(t, true);
                if (preE.Hitchance >= hit)
                {
                    Spell[SpellSlot.E].Cast(sourcePosition, preE.CastPosition);
                }
            }
            else if (Player.Distance(t.ServerPosition) < Spells.ECastRange + Spell[SpellSlot.E].Range &&
                     Spell[SpellSlot.E].IsReady())
            {
                var sourcePosition = Player.ServerPosition.Extend(t.ServerPosition, Spells.ECastRange);
                Spell[SpellSlot.E].UpdateSourcePosition(sourcePosition, sourcePosition);
                var preE = Spell[SpellSlot.E].GetPrediction(t, true);
                if (preE.Hitchance >= hit)
                {
                    Spell[SpellSlot.E].Cast(sourcePosition, preE.CastPosition);
                }
            }
        }

        private static void CastR(Obj_AI_Base t)
        {
            if (t == null || !Spell[SpellSlot.R].IsReady() || ChaosStorm != null)
            {
                return;
            }


            var preR = Spell[SpellSlot.R].GetPrediction(t, true);
            if (t.IsValidTarget(Spell[SpellSlot.R].Range) &&
                Config.ViktorConfig.Item("apollo.viktor.combo.r.kill").GetValue<bool>() &&
                Damages.ComboDmg(t) > t.Health &&
                t.HealthPercent > Config.ViktorConfig.Item("apollo.viktor.combo.r.minhp").GetValue<Slider>().Value)
            {
                Spell[SpellSlot.R].Cast(t, PacketCast, true);
            }
            else if (preR.AoeTargetsHitCount >=
                     Config.ViktorConfig.Item("apollo.viktor.combo.r.hit").GetValue<Slider>().Value)
            {
                Spell[SpellSlot.R].Cast(preR.CastPosition, PacketCast);
            }
        }

        private static void AutoFollowR()
        {
            if (ChaosStorm != null)
            {
                var stormT =
                    HeroManager.Enemies.Where(h => h.IsValid && h.ServerPosition.Distance(ChaosStorm.Position) < 1500)
                        .OrderBy(h => TargetSelector.GetPriority(h))
                        .FirstOrDefault();
                if (stormT != null)
                    Utility.DelayAction.Add(600, () => Spell[SpellSlot.R].Cast(stormT.ServerPosition));
            }
        }

        private static void KillSteal()
        {
            var useE = Config.ViktorConfig.Item("apollo.viktor.ks.e.bool").GetValue<bool>();
            var t =
                HeroManager.Enemies.Where(
                    h =>
                        h.IsValidTarget(Spells.ECastRange + Spell[SpellSlot.E].Range) &&
                        h.Health + 15 < Player.GetSpellDamage(h, SpellSlot.E))
                    .OrderBy(h => h.Health)
                    .FirstOrDefault(h => h.Health > 15);

            if (t == null)
                return;

            if (useE && Spell[SpellSlot.E].IsReady())
            {
                if (Player.Distance(t) < Spells.ECastRange)
                {
                    var sourcePosition = t.ServerPosition;
                    Spell[SpellSlot.E].UpdateSourcePosition(sourcePosition, sourcePosition);
                    var preE = Spell[SpellSlot.E].GetPrediction(t, true);

                    Spell[SpellSlot.E].Cast(sourcePosition, preE.CastPosition);
                }
                else
                {
                    var sourcePosition = Player.ServerPosition.Extend(t.ServerPosition, Spells.ECastRange);
                    Spell[SpellSlot.E].UpdateSourcePosition(sourcePosition, sourcePosition);
                    var preE = Spell[SpellSlot.E].GetPrediction(t, true);

                    Spell[SpellSlot.E].Cast(sourcePosition, preE.CastPosition);
                }
            }
        }

        private static void OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (Config.ViktorConfig.Item("apollo.viktor.gapcloser.w.bool").GetValue<bool>() &&
                Player.Distance(gapcloser.Sender) < Orbwalking.GetRealAutoAttackRange(Player) &&
                Spell[SpellSlot.W].IsReady())
            {
                Spell[SpellSlot.W].Cast(gapcloser.End, PacketCast);
            }
        }

        private static void OnInterruptableTarget(Obj_AI_Hero unit, Interrupter2.InterruptableTargetEventArgs args)
        {
            if (args.DangerLevel >= Interrupter2.DangerLevel.High)
            {
                bool useW = Config.ViktorConfig.Item("apollo.viktor.interrupt.w.bool").GetValue<bool>();
                bool useR = Config.ViktorConfig.Item("apollo.viktor.interrupt.r.bool").GetValue<bool>();

                if (useW && Spell[SpellSlot.W].IsReady() && unit.IsValidTarget(Spell[SpellSlot.W].Range) &&
                    (Game.Time + 1.5 + Spell[SpellSlot.W].Delay) >= args.EndTime)
                {
                    Spell[SpellSlot.W].Cast(unit.ServerPosition, PacketCast);
                }
                else if (useR && unit.IsValidTarget(Spell[SpellSlot.R].Range))
                {
                    Spell[SpellSlot.R].Cast(unit.ServerPosition, PacketCast);
                }
            }
        }

        private static void OnCreate(GameObject sender, EventArgs args)
        {
            if (!sender.IsValid)
            {
                return;
            }

            if (sender.Name.Contains("Viktor_Base_R_Droid.troy"))
            {
                ChaosStorm = sender;
            }
        }

        private static void OnDelete(GameObject sender, EventArgs args)
        {
            if (!sender.IsValid)
            {
                return;
            }

            if (sender.Name.Contains("Viktor_Base_R_Droid.troy"))
            {
                ChaosStorm = null;
            }
        }
    }
}