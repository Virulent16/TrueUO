using Server.Engines.SeasonalEvents;
using Server.Engines.TreasuresOfKotlCity;
using Server.Items;
using Server.Mobiles;
using System;
using System.Collections.Generic;

namespace Server.Engines.Points
{
    public class KotlCityData : PointsSystem
    {
        public override PointsType Loyalty => PointsType.TreasuresOfKotlCity;
        public override TextDefinition Name => m_Name;
        public override bool AutoAdd => true;
        public override double MaxPoints => double.MaxValue;
        public override bool ShowOnLoyaltyGump => false;

        private readonly TextDefinition m_Name = null;

        public bool Enabled => SeasonalEventSystem.IsActive(EventType.TreasuresOfKotlCity);

        public KotlCityData()
        {
            DungeonPoints = new Dictionary<Mobile, int>();
        }

        public override void SendMessage(PlayerMobile from, double old, double points, bool quest)
        {
            from.SendLocalizedMessage(1156902, ((int)points).ToString()); // You have turned in ~1_COUNT~ artifacts of the Kotl
        }

        public override void ProcessKill(Mobile victim, Mobile damager)
        {
            BaseCreature bc = victim as BaseCreature;

            if (!Enabled || bc == null || bc.Controlled || bc.Summoned || !damager.Alive)
                return;

            Region r = bc.Region;

            if (damager is PlayerMobile mobile && r.IsPartOf("KotlCity"))
            {
                if (!DungeonPoints.ContainsKey(mobile))
                    DungeonPoints[mobile] = 0;

                int fame = bc.Fame / 2;
                int luck = Math.Max(0, mobile.RealLuck);

                if (bc.Spawner is KotlBattleSimulator)
                {
                    fame *= 4;
                }

                DungeonPoints[mobile] += (int)(fame * (1 + Math.Sqrt(luck) / 100));

                int x = DungeonPoints[mobile];
                const double A = 0.000863316841;
                const double B = 0.00000425531915;

                double chance = A * Math.Pow(10, B * x);

                if (chance > Utility.RandomDouble())
                {
                    Item i = Loot.RandomArmorOrShieldOrWeaponOrJewelry(LootPackEntry.IsInTokuno(bc), LootPackEntry.IsMondain(bc), LootPackEntry.IsStygian(bc));

                    if (i != null)
                    {
                        RunicReforging.GenerateRandomItem(i, mobile, Math.Max(100, RunicReforging.GetDifficultyFor(bc)), LootPack.GetLuckChance(mobile.RealLuck), ReforgedPrefix.None, ReforgedSuffix.Kotl);

                        mobile.PlaySound(0x5B4);
                        mobile.SendLocalizedMessage(1062317); // For your valor in combating the fallen beast, a special artifact has been bestowed on you.

                        if (!mobile.PlaceInBackpack(i))
                        {
                            if (mobile.BankBox != null && mobile.BankBox.TryDropItem(mobile, i, false))
                                mobile.SendLocalizedMessage(1079730); // The item has been placed into your bank box.
                            else
                            {
                                mobile.SendLocalizedMessage(1072523); // You find an artifact, but your backpack and bank are too full to hold it.
                                i.MoveToWorld(mobile.Location, mobile.Map);
                            }
                        }

                        DungeonPoints.Remove(mobile);
                    }
                }
            }
        }

        public Dictionary<Mobile, int> DungeonPoints { get; }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(1);

            writer.Write(DungeonPoints.Count);
            foreach (KeyValuePair<Mobile, int> kvp in DungeonPoints)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value);
            }
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);

            int version = reader.ReadInt();

            if (version == 0)
            {
                reader.ReadBool();
            }

            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                Mobile m = reader.ReadMobile();
                int points = reader.ReadInt();

                if (m != null && points > 0)
                    DungeonPoints[m] = points;
            }
        }
    }
}
