using Server.Engines.Quests;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Spells;
using Server.Targeting;
using System;

namespace Server.Items
{
    public class SpecialSalvageHook : Item
    {
        private static readonly int[] m_Hues =
        {
            0x09B,
            0x0CD,
            0x0D3,
            0x14D,
            0x1DD,
            0x1E9,
            0x1F4,
            0x373,
            0x451,
            0x47F,
            0x489,
            0x492,
            0x4B5,
            0x8AA
        };

        private static readonly int[] m_WaterTiles =
        {
            0x00A8, 0x00AB,
            0x0136, 0x0137
        };

        private static readonly int[] m_UndeepWaterTiles =
        {
            0x1797, 0x179C
        };

        private bool m_InUse;
        private int _Tick;
        private Timer _EffectTimer;

        [Constructable]
        public SpecialSalvageHook()
              : base(0x14F7)
        {
            Weight = 25.0;
            Hue = 2654;
        }

        public SpecialSalvageHook(Serial serial)
            : base(serial)
        {
        }

        public override int LabelNumber => 1154215;  // A Special Salvage Hook

        [CommandProperty(AccessLevel.GameMaster)]
        public bool InUse { get => m_InUse; set => m_InUse = value; }

        public virtual bool RequireDeepWater => true;

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);

            writer.Write(1); // version

            writer.Write(m_InUse);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);

            int version = reader.ReadInt();

            switch (version)
            {
                case 1:
                    {
                        m_InUse = reader.ReadBool();

                        if (m_InUse)
                            Delete();

                        break;
                    }
            }

            Stackable = false;
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (from is PlayerMobile pm && pm.ExploringTheDeepQuest > ExploringTheDeepQuestChain.None)
            {
                if (!m_InUse)
                {
                    from.SendLocalizedMessage(1154219); // Where do you wish to use this?
                    from.BeginTarget(-1, true, TargetFlags.None, OnTarget);
                }
            }
            else
            {
                from.PublicOverheadMessage(MessageType.Regular, 0x3B2, 1154274); // *You aren't quite sure what to do with this. If you spoke to the Salvage Master at the Sons of the Sea in Trinsic you might have a better understanding of its use...*
            }

        }

        public void OnTarget(Mobile from, object obj)
        {
            if (Deleted || m_InUse)
                return;

            IPoint3D p3D = obj as IPoint3D;

            if (p3D == null)
                return;

            Map map = from.Map;

            if (map == null || map == Map.Internal)
                return;

            int x = p3D.X, y = p3D.Y, z = map.GetAverageZ(x, y); // OSI just takes the targeted Z

            if (!from.InLOS(obj))
            {
                from.SendLocalizedMessage(500979); // You cannot see that location.
            }
            else if (RequireDeepWater ? SpecialFishingNet.FullValidation(map, x, y) : SpecialFishingNet.ValidateDeepWater(map, x, y) || SpecialFishingNet.ValidateUndeepWater(map, obj, ref z))
            {
                Point3D p = new Point3D(x, y, z);

                if (GetType() == typeof(SpecialSalvageHook))
                {
                    for (int i = 1; i < Amount; ++i) // these were stackable before, doh
                        from.AddToBackpack(new SpecialSalvageHook());
                }

                _Tick = 0;

                m_InUse = true;
                Movable = false;
                MoveToWorld(p, map);

                SpellHelper.Turn(from, p);
                from.Animate(12, 5, 1, true, false, 0);

                Effects.SendLocationEffect(p, map, 0x352D, 16, 4);
                Effects.PlaySound(p, map, 0x364);

                _EffectTimer = Timer.DelayCall(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.25), new TimerStateCallback(DoEffect), new object[] { p, from });
                _EffectTimer.Start();

                from.PublicOverheadMessage(MessageType.Regular, 0x3B2, 1154220); // *You cast the mighty hook into the sea!*
            }
            else
            {
                from.SendLocalizedMessage(1010485); // You can only use this in deep water!
            }
        }

        protected virtual int GetSpawnCount()
        {
            int count = Utility.RandomMinMax(1, 3);

            if (Hue != 0x8A0)
                count += Utility.RandomMinMax(1, 2);

            return count;
        }

        private static void Spawn(Point3D p, Map map, ISpawnable spawn)
        {
            if (map == null)
            {
                spawn.Delete();
                return;
            }

            int x = p.X, y = p.Y;

            for (int j = 0; j < 20; ++j)
            {
                int tx = p.X - 2 + Utility.Random(5);
                int ty = p.Y - 2 + Utility.Random(5);

                LandTile t = map.Tiles.GetLandTile(tx, ty);

                if (t.Z == p.Z && (t.ID >= 0xA8 && t.ID <= 0xAB || t.ID >= 0x136 && t.ID <= 0x137) && !SpellHelper.CheckMulti(new Point3D(tx, ty, p.Z), map))
                {
                    x = tx;
                    y = ty;
                    break;
                }
            }

            spawn.MoveToWorld(new Point3D(x, y, p.Z), map);
        }

        protected virtual void SpawnBaddies(Point3D p, Map map, Mobile from)
        {
            if (from != null && map != null)
            {
                from.RevealingAction();

                int count = GetSpawnCount();

                for (int i = 0; i < count; ++i)
                {
                    BaseCreature spawn;

                    switch (Utility.Random(4))
                    {
                        default:
                        case 0:
                            spawn = new SeaSerpent();
                            break;
                        case 1:
                            spawn = new DeepSeaSerpent();
                            break;
                        case 2:
                            spawn = new WaterElemental();
                            break;
                        case 3:
                            spawn = new Kraken();
                            break;
                    }

                    Spawn(p, map, spawn);
                    spawn.Combatant = from;
                }
            }
        }

        private static bool ValidateUndeepWater(Map map, object obj, ref int z)
        {
            if (!(obj is StaticTarget))
                return false;

            StaticTarget target = (StaticTarget)obj;

            if (BaseHouse.FindHouseAt(target.Location, map, 0) != null)
                return false;

            int itemID = target.ItemID;

            for (int i = 0; i < m_UndeepWaterTiles.Length; i += 2)
            {
                if (itemID >= m_UndeepWaterTiles[i] && itemID <= m_UndeepWaterTiles[i + 1])
                {
                    z = target.Z;
                    return true;
                }
            }

            return false;
        }

        private void DoEffect(object state)
        {
            if (Deleted)
                return;

            object[] states = (object[])state;

            Point3D p = (Point3D)states[0];
            Mobile from = (Mobile)states[1];

            if (_Tick == 1)
            {
                Effects.SendLocationEffect(p, Map, 0x352D, 16, 4);
                Effects.PlaySound(p, Map, 0x364);
            }
            else if (_Tick <= 7 || _Tick == 14)
            {
                if (RequireDeepWater)
                {
                    for (int i = 0; i < 3; ++i)
                    {
                        int x, y = 0;

                        do
                        {
                            x = Utility.RandomMinMax(-1, 1);
                            y = Utility.RandomMinMax(-1, 1);
                        }
                        while (x == 0 && y == 0);

                        Effects.SendLocationEffect(new Point3D(p.X + x, p.Y + y, p.Z), Map, 0x352D, 16, 4);
                    }

                    if (_Tick == 14)
                    {
                        if (0.6 >= Utility.RandomDouble())
                        {
                            if (0.5 >= Utility.RandomDouble())
                            {
                                from.PublicOverheadMessage(MessageType.Regular, 0x3B2, 1154218); // *The line snaps tight as you snare a piece of wreckage from the sea floor!*

                                from.AddToBackpack(new BrokenShipwreckRemains());
                            }
                            else
                            {
                                SpawnBaddies(p, Map, from);
                            }
                        }

                        //TODO: Message?

                        Delete();
                    }
                }
                else
                {
                    Effects.SendLocationEffect(p, Map, 0x352D, 16, 4);
                }

                if (Utility.RandomBool())
                    Effects.PlaySound(p, Map, 0x364);

                Z -= 1;
            }

            _Tick++;
        }

        public override void Delete()
        {
            base.Delete();

            if (_EffectTimer != null)
            {
                _EffectTimer.Stop();
                _EffectTimer = null;
            }
        }
    }

}
