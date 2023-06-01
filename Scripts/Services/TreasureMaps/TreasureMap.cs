#region References
using Server.ContextMenus;
using Server.Engines.CannedEvil;
using Server.Engines.Harvest;
using Server.Misc;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Regions;
using Server.Spells;
using Server.Targeting;
using System;
using System.Collections.Generic;
#endregion

namespace Server.Items
{
    public class TreasureMap : MapItem
    {
        public static double LootChance = Config.Get("TreasureMaps.LootChance", .01);
        private static TimeSpan ResetTime = TimeSpan.FromDays(Config.Get("TreasureMaps.ResetTime", 30.0));

        #region Forgotten Treasures
        private TreasurePackage _Package;

        [CommandProperty(AccessLevel.GameMaster)]
        public TreasureLevel TreasureLevel
        {
            get => (TreasureLevel)m_Level;
            set
            {
                if ((int)value != Level)
                {
                    Level = (int)value;
                }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public TreasurePackage Package { get => _Package; set { _Package = value; InvalidateProperties(); } }

        [CommandProperty(AccessLevel.GameMaster)]
        public TreasureFacet TreasureFacet => TreasureMapInfo.GetFacet(ChestLocation, Facet);

        protected void AssignRandomPackage()
        {
            Package = (TreasurePackage)Utility.Random(5);
        }

        public void AssignChestQuality(Mobile digger, TreasureMapChest chest)
        {
            double skill = digger.Skills[SkillName.Cartography].Value;

            int dif;

            switch (TreasureLevel)
            {
                default:
                case TreasureLevel.Stash: dif = 100; break;
                case TreasureLevel.Supply: dif = 200; break;
                case TreasureLevel.Cache: dif = 300; break;
                case TreasureLevel.Hoard: dif = 400; break;
                case TreasureLevel.Trove: dif = 500; break;
            }

            if (Utility.Random(dif) <= skill)
            {
                chest.ChestQuality = ChestQuality.Gold;
            }
            else if (Utility.Random(dif) <= skill * 2)
            {
                chest.ChestQuality = ChestQuality.Standard;
            }
            else
            {
                chest.ChestQuality = ChestQuality.Rusty;
            }
        }
        #endregion

        #region Spawn Types
        private static readonly Type[][] m_SpawnTypes =
        {
            new[]{ typeof(Mongbat), typeof(Ratman), typeof(HeadlessOne), typeof(Skeleton), typeof(Zombie) },
            new[]{ typeof(AirElemental), typeof(DreadSpider), typeof(EarthElemental), typeof(FireElemental), typeof(Gargoyle), typeof(Gazer), typeof(HellHound), typeof(Lich), typeof(EvilMage), typeof(OgreLord), typeof(Orc) },
            new[]{ typeof(BloodElemental), typeof(Daemon), typeof(DreadSpider), typeof(ElderGazer), typeof(LichLord), typeof(OgreLord), typeof(PoisonElemental) },
            new[]{ typeof(AncientWyrm), typeof(Balron), typeof(BloodElemental), typeof(PoisonElemental), typeof(Titan), typeof(GreaterDragon), typeof(ColdDrake), typeof(DragonWolf) },
            new[]{ typeof(AncientWyrm), typeof(Balron), typeof(BloodElemental), typeof(PoisonElemental), typeof(Titan), typeof(GreaterDragon), typeof(ColdDrake), typeof(FrostDragon) },
        };

        private static readonly Type[][] m_TokunoSpawnTypes =
        {
            new[]{ typeof(Mongbat), typeof(Ratman), typeof(HeadlessOne), typeof(Skeleton), typeof(Zombie)  },
            new[]{ typeof(AirElemental), typeof(DreadSpider), typeof(EarthElemental), typeof(FireElemental), typeof(Gargoyle), typeof(Gazer), typeof(HellHound), typeof(Lich), typeof(EvilMage), typeof(OgreLord), typeof(Orc) },
            new[]{ typeof(Daemon), typeof(Devourer), typeof(DreadSpider), typeof(ElderGazer), typeof(FanDancer), typeof(LichLord), typeof(OgreLord), typeof(RevenantLion), typeof(Ronin), typeof(RuneBeetle) },
            new[]{ typeof(RuneBeetle), typeof(LadyOfTheSnow), typeof(YomotsuElder), typeof(YomotsuPriest), typeof(YomotsuWarrior), typeof(Hiryu), typeof(Oni), typeof(DragonWolf) },
            new[]{ typeof(RuneBeetle), typeof(LadyOfTheSnow), typeof(YomotsuElder), typeof(YomotsuPriest), typeof(YomotsuWarrior), typeof(Hiryu), typeof(Oni), typeof(Yamandon) }
        };

        private static readonly Type[][] m_MalasSpawnTypes =
        {
            new[]{ typeof(Mongbat), typeof(Ratman), typeof(HeadlessOne), typeof(Skeleton), typeof(Zombie) },
            new[]{ typeof(AirElemental), typeof(DreadSpider), typeof(EarthElemental), typeof(FireElemental), typeof(Gargoyle), typeof(Gazer), typeof(HellHound), typeof(Lich), typeof(EvilMage), typeof(OgreLord), typeof(Orc) },
            new[]{ typeof(Daemon), typeof(DreadSpider), typeof(ElderGazer), typeof(LichLord), typeof(OgreLord), typeof(Ravager), typeof(WandererOfTheVoid) },
            new[]{ typeof(Devourer), typeof(RottingCorpse), typeof(WandererOfTheVoid), typeof(MinotaurCaptain), typeof(MinotaurScout), typeof(DragonWolf)  },
            new[]{ typeof(Devourer), typeof(RottingCorpse), typeof(WandererOfTheVoid), typeof(MinotaurCaptain), typeof(MinotaurScout), typeof(MinotaurGeneral) }
        };

        private static readonly Type[][] m_IlshenarSpawnTypes =
        {
            new[]{ typeof(Mongbat), typeof(Ratman), typeof(HeadlessOne), typeof(Skeleton), typeof(Zombie) },
            new[]{ typeof(AirElemental), typeof(DreadSpider), typeof(EarthElemental), typeof(FireElemental), typeof(Gargoyle), typeof(Gazer), typeof(HellHound), typeof(Lich), typeof(EvilMage), typeof(OgreLord), typeof(Orc) },
            new[]{ typeof(Daemon), typeof(DarkGuardian), typeof(DreadSpider), typeof(ElderGazer), typeof(ExodusMinion), typeof(GargoyleDestroyer), typeof(GargoyleEnforcer), typeof(LichLord), typeof(OgreLord), typeof(PoisonElemental) },
            new[]{ typeof(ExodusMinion), typeof(GargoyleDestroyer), typeof(Titan), typeof(Changeling), typeof(EnslavedSatyr), typeof(DragonWolf) },
            new[]{ typeof(ExodusMinion), typeof(GargoyleDestroyer), typeof(Titan), typeof(RenegadeChangeling), typeof(EnslavedSatyr) }
        };

        private static readonly Type[][] m_TerMurSpawnTypes =
        {
            new[]{ typeof(Mongbat), typeof(Ratman), typeof(HeadlessOne), typeof(Skeleton), typeof(ClockworkScorpion), typeof(CorrosiveSlime)  },
            new[]{ typeof(AcidSlug), typeof(Slith), typeof(WaterElemental), typeof(LeatherWolf), typeof(StoneSlith), typeof(ToxicSlith) },
            new[]{ typeof(BloodWorm), typeof(FireAnt), typeof(Kepetch), typeof(LavaElemental), typeof(MaddeningHorror), typeof(StoneSlith), typeof(ToxicSlith) },
            new[]{ typeof(LavaElemental), typeof(GreaterPoisonElemental), typeof(EnragedEarthElemental), typeof(FireDaemon) },
            new[]{ typeof(LavaElemental), typeof(GreaterPoisonElemental), typeof(EnragedEarthElemental), typeof(FireDaemon), typeof(EnragedColossus) }
        };

        private static readonly Type[][] m_EodonSpawnTypes =
        {
            new[] { typeof(MyrmidexLarvae), typeof(SilverbackGorilla), typeof(Panther), typeof(WildTiger) },
            new[] { typeof(AcidElemental), typeof(SandVortex), typeof(Lion), typeof(SabreToothedTiger) },
            new[] { typeof(Infernus), typeof(FireElemental), typeof(Dimetrosaur), typeof(Saurosaurus) },
            new[] { typeof(KotlAutomaton), typeof(MyrmidexDrone), typeof(Allosaurus), typeof(Triceratops) },
            new[] { typeof(Anchisaur), typeof(Allosaurus), typeof(SandVortex) }
        };
        #endregion

        #region Spawn Locations
        private static readonly Rectangle2D[] m_FelTramWrap =
        {
            new Rectangle2D(0, 0, 5119, 4095)
        };

        private static readonly Rectangle2D[] m_TokunoWrap =
        {
            new Rectangle2D(155, 207, 30, 40),
            new Rectangle2D(280, 230, 157, 45),
            new Rectangle2D(445, 215, 30, 35 ),
            new Rectangle2D(447, 53, 58, 40),
            new Rectangle2D(612, 240, 20, 17),
            new Rectangle2D(167, 275, 53, 60),
            new Rectangle2D(734, 407, 14, 22),
            new Rectangle2D(753, 489, 8, 30),
            new Rectangle2D(624, 619, 20, 24),
            new Rectangle2D(624, 725, 8, 8),
            new Rectangle2D(574, 734, 20, 16),
            new Rectangle2D(431, 752, 25, 27),
            new Rectangle2D(348, 968, 52, 135),
            new Rectangle2D(282, 1188, 90, 100),
            new Rectangle2D(348, 1335, 50, 50),

            new Rectangle2D(228, 284, 500, 316),
            new Rectangle2D(95, 600, 345, 243),
            new Rectangle2D(155, 842, 146, 1358),
            new Rectangle2D(495, 812, 435, 350),
            new Rectangle2D(501, 1156, 100, 150),
            new Rectangle2D(876, 1156, 90, 150),

            new Rectangle2D(970, 1159, 14, 25),
            new Rectangle2D(990, 1151, 5, 15),
            new Rectangle2D(1004, 1120, 16, 30),
            new Rectangle2D(1008, 1032, 12, 15),
            new Rectangle2D(1163, 383, 20, 20),

            new Rectangle2D(839, 30, 168, 120),
            new Rectangle2D(707, 150, 307, 250),
            new Rectangle2D(845, 397, 179, 75),
            new Rectangle2D(1068, 382, 60, 80),
            new Rectangle2D(787, 687, 60, 72),
            new Rectangle2D(848, 473, 557, 655)
        };

        private static readonly Rectangle2D[] m_MalasWrap =
        {
            new Rectangle2D(611, 67, 1862, 705),
            new Rectangle2D(1540, 852, 286, 182),
            new Rectangle2D(602, 784, 546, 746),
            new Rectangle2D(1160, 1035, 1299, 871)
        };

        private static readonly Rectangle2D[] m_IlshenarWrap =
        {
            new Rectangle2D(221, 314, 657, 286),
            new Rectangle2D(530, 600, 212, 205),
            new Rectangle2D(261, 805, 495, 655),
            new Rectangle2D(908, 925, 90, 170),
            new Rectangle2D(1031, 904, 730, 450),
            new Rectangle2D(1028, 630, 318, 161),
            new Rectangle2D(1205, 368, 265, 237),
            new Rectangle2D(1551, 516, 200, 130)
        };

        private static readonly Rectangle2D[] m_TerMurWrap =
        {
            new Rectangle2D(535, 2895, 85, 117),
            new Rectangle2D(525, 3085, 115, 70),
            new Rectangle2D(755, 2860, 400, 270),
            new Rectangle2D(1025, 3280, 190, 100 ),
            new Rectangle2D(305, 3445, 175, 255),
            new Rectangle2D(480, 3540, 90, 110),
            new Rectangle2D(605, 3880, 200, 170),
            new Rectangle2D(750, 3830, 80, 80)
        };

        private static readonly Rectangle2D[] m_EodonWrap =
        {
            new Rectangle2D(259, 1400, 354, 510),
            new Rectangle2D(259, 1400, 354, 510),
            new Rectangle2D(259, 1400, 354, 510),
            new Rectangle2D(688, 1440, 46, 88),
            new Rectangle2D(613, 1466, 65, 139),
            new Rectangle2D(678, 1568, 43, 40),
            new Rectangle2D(613, 1720, 91, 72),
            new Rectangle2D(618, 1792, 44, 273),
            new Rectangle2D(662, 1969, 84, 166),
            new Rectangle2D(754, 1963, 100, 65),
            new Rectangle2D(174, 1540, 85, 420)
        };
        #endregion

        private int m_Level;
        private bool m_Completed;
        private Mobile m_CompletedBy;
        private Mobile m_Decoder;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Level
        {
            get => m_Level;
            set
            {
                m_Level = Math.Min(value, 4);
                InvalidateProperties();
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Completed
        {
            get => m_Completed;
            set
            {
                m_Completed = value;
                InvalidateProperties();
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public Mobile CompletedBy
        {
            get => m_CompletedBy;
            set
            {
                m_CompletedBy = value;
                InvalidateProperties();
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public Mobile Decoder
        {
            get => m_Decoder;
            set
            {
                m_Decoder = value;
                InvalidateProperties();
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public Point2D ChestLocation { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime NextReset { get; set; }

        public override int LabelNumber
        {
            get
            {
                if (m_Decoder != null)
                {
                    return 1041516 + m_Level;
                }

                return 1041510 + m_Level;
            }
        }

        public TreasureMap()
        {
        }

        [Constructable]
        public TreasureMap(int level, Map map)
            : this(level, map, false)
        {
        }

        [Constructable]
        public TreasureMap(int level, Map map, bool eodon)
        {
            Level = level;

            AssignRandomPackage();

            if (map == Map.Internal)
            {
                map = GetRandomMap();
            }

            Facet = map;
            ChestLocation = GetRandomLocation(map, eodon);

            Width = 300;
            Height = 300;
            int width, height;

            GetWidthAndHeight(map, out width, out height);

            int x1 = ChestLocation.X - Utility.RandomMinMax(width / 4, width / 4 * 3);
            int y1 = ChestLocation.Y - Utility.RandomMinMax(height / 4, height / 4 * 3);

            if (x1 < 0)
                x1 = 0;

            if (y1 < 0)
                y1 = 0;

            int x2;
            int y2;

            AdjustMap(map, out x2, out y2, x1, y1, width, height, eodon);

            x1 = x2 - width;
            y1 = y2 - height;

            Bounds = new Rectangle2D(x1, y1, width, height);
            Protected = true;

            AddWorldPin(ChestLocation.X, ChestLocation.Y);

            NextReset = DateTime.UtcNow + ResetTime;
        }

        public Map GetRandomMap()
        {
            switch (Utility.Random(8))
            {
                default:
                case 0: return Map.Trammel;
                case 1: return Map.Felucca;
                case 2:
                case 3: return Map.Ilshenar;
                case 4:
                case 5: return Map.Malas;
                case 6:
                case 7: return Map.Tokuno;
            }
        }

        public static Point2D GetRandomLocation(Map map)
        {
            return GetRandomLocation(map, false);
        }

        public static bool IsTameable(BaseCreature bc)
        {
            for (var index = 0; index < m_TameableCreatures.Length; index++)
            {
                var t = m_TameableCreatures[index];

                if (t == bc.GetType())
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly Type[] m_TameableCreatures =
        {
            typeof(Panther), typeof(WildTiger), typeof(Lion), typeof(SabreToothedTiger),
            typeof(Dimetrosaur), typeof(Saurosaurus), typeof(Triceratops), typeof(DragonWolf),
            typeof(FrostDragon)
        };

        public static Point2D GetRandomLocation(Map map, bool eodon)
        {
            Rectangle2D[] recs;

            int x = 0;
            int y = 0;

            if (map == Map.Trammel || map == Map.Felucca)
                recs = m_FelTramWrap;
            else if (map == Map.Tokuno)
                recs = m_TokunoWrap;
            else if (map == Map.Malas)
                recs = m_MalasWrap;
            else if (map == Map.Ilshenar)
                recs = m_IlshenarWrap;
            else if (eodon)
                recs = m_EodonWrap;
            else
                recs = m_TerMurWrap;

            while (true)
            {
                Rectangle2D rec = recs[Utility.Random(recs.Length)];

                x = Utility.Random(rec.X, rec.Width);
                y = Utility.Random(rec.Y, rec.Height);

                if (ValidateLocation(x, y, map))
                    return new Point2D(x, y);
            }
        }

        public static bool ValidateLocation(int x, int y, Map map)
        {
            LandTile lt = map.Tiles.GetLandTile(x, y);
            LandData ld = TileData.LandTable[lt.ID];

            //Checks for impassable flag..cant walk, cant have a chest
            if (lt.Ignored || (ld.Flags & TileFlag.Impassable) > 0)
            {
                return false;
            }

            //Checks for roads
            for (int i = 0; i < HousePlacement.RoadIDs.Length; i += 2)
            {
                if (lt.ID >= HousePlacement.RoadIDs[i] && lt.ID <= HousePlacement.RoadIDs[i + 1])
                {
                    return false;
                }
            }

            Region reg = Region.Find(new Point3D(x, y, lt.Z), map);

            //no-go in towns, houses, dungeons and champspawns
            if (reg != null)
            {
                if (reg.IsPartOf<TownRegion>() || reg.IsPartOf<DungeonRegion>() || reg.IsPartOf<ChampionSpawnRegion>() || reg.IsPartOf<HouseRegion>())
                {
                    return false;
                }
            }

            string n = (ld.Name ?? string.Empty).ToLower();

            if (n != "dirt" && n != "grass" && n != "jungle" && n != "forest" && n != "snow")
            {
                return false;
            }

            //Rare occrunces where a static tile needs to be checked
            var tiles = map.Tiles.GetStaticTiles(x, y, true);

            for (var index = 0; index < tiles.Length; index++)
            {
                StaticTile tile = tiles[index];
                ItemData td = TileData.ItemTable[tile.ID & TileData.MaxItemValue];

                if ((td.Flags & TileFlag.Impassable) > 0)
                {
                    return false;
                }

                n = (td.Name ?? string.Empty).ToLower();

                if (n != "dirt" && n != "grass" && n != "jungle" && n != "forest" && n != "snow")
                {
                    return false;
                }
            }

            //check for house within 5 tiles
            for (int xx = x - 5; xx <= x + 5; xx++)
            {
                for (int yy = y - 5; yy <= y + 5; yy++)
                {
                    if (BaseHouse.FindHouseAt(new Point3D(xx, yy, lt.Z), map, Region.MaxZ - lt.Z) != null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public void GetWidthAndHeight(Map map, out int width, out int height)
        {
            if (map == Map.Trammel || map == Map.Felucca)
            {
                width = 600;
                height = 600;
            }
            else if (map == Map.TerMur)
            {
                width = 200;
                height = 200;
            }
            else
            {
                width = 300;
                height = 300;
            }
        }

        public void AdjustMap(Map map, out int x2, out int y2, int x1, int y1, int width, int height)
        {
            AdjustMap(map, out x2, out y2, x1, y1, width, height, false);
        }

        public void AdjustMap(Map map, out int x2, out int y2, int x1, int y1, int width, int height, bool eodon)
        {
            x2 = x1 + width;
            y2 = y1 + height;

            if (map == Map.Trammel || map == Map.Felucca)
            {
                if (x2 >= 5120)
                    x2 = 5119;

                if (y2 >= 4096)
                    y2 = 4095;
            }
            else if (map == Map.Ilshenar)
            {
                if (x2 >= 1890)
                    x2 = 1889;

                if (x2 <= 120)
                    x2 = 121;

                if (y2 >= 1465)
                    y2 = 1464;

                if (y2 <= 105)
                    y2 = 106;
            }
            else if (map == Map.Malas)
            {
                if (x2 >= 2522)
                    x2 = 2521;

                if (x2 <= 515)
                    x2 = 516;

                if (y2 >= 1990)
                    y2 = 1989;

                if (y2 <= 0)
                    y2 = 1;
            }
            else if (map == Map.Tokuno)
            {
                if (x2 >= 1428)
                    x2 = 1427;

                if (x2 <= 0)
                    x2 = 1;

                if (y2 >= 1420)
                    y2 = 1419;

                if (y2 <= 0)
                    y2 = 1;
            }
            else if (map == Map.TerMur)
            {
                if (eodon)
                {
                    if (x2 <= 62)
                        x2 = 63;

                    if (x2 >= 960)
                        x2 = 959;

                    if (y2 <= 1343)
                        y2 = 1344;

                    if (y2 >= 2240)
                        y2 = 2239;
                }
                else
                {
                    if (x2 >= 1271)
                        x2 = 1270;

                    if (x2 <= 260)
                        x2 = 261;

                    if (y2 >= 4094)
                        y2 = 4083;

                    if (y2 <= 2760)
                        y2 = 2761;
                }
            }
        }

        public virtual void OnMapComplete(Mobile from, TreasureMapChest chest)
        {
        }

        public virtual void OnChestOpened(Mobile from, TreasureMapChest chest)
        {
        }

        public TreasureMap(Serial serial)
            : base(serial)
        { }

        public static BaseCreature Spawn(int level, Point3D p, Map map, bool guardian)
        {
            Type[][] spawns;

            if (map == Map.Trammel || map == Map.Felucca)
                spawns = m_SpawnTypes;
            else if (map == Map.Tokuno)
                spawns = m_TokunoSpawnTypes;
            else if (map == Map.Ilshenar)
                spawns = m_IlshenarSpawnTypes;
            else if (map == Map.Malas)
                spawns = m_MalasSpawnTypes;
            else
            {
                if (SpellHelper.IsEodon(map, p))
                {
                    spawns = m_EodonSpawnTypes;
                }
                else
                {
                    spawns = m_TerMurSpawnTypes;
                }
            }

            if (level >= 0 && level < spawns.Length)
            {
                BaseCreature bc;

                try
                {
                    bc = (BaseCreature)Activator.CreateInstance(spawns[level][Utility.Random(spawns[level].Length)]);
                }
                catch (Exception e)
                {
                    Diagnostics.ExceptionLogging.LogException(e);
                    return null;
                }

                bc.Home = p;
                bc.RangeHome = 5;

                if (guardian && !IsTameable(bc))
                {
                    bc.Title = "(Guardian)";
                    bc.Tamable = false;

                    if (BaseCreature.IsSoulboundEnemies)
                    {
                        bc.IsSoulBound = true;
                    }
                }

                return bc;
            }

            return null;
        }

        public static BaseCreature Spawn(int level, Point3D p, Map map, Mobile target, bool guardian)
        {
            if (map == null)
                return null;

            BaseCreature bc = Spawn(level, p, map, guardian);

            if (bc != null)
            {
                bool spawned = false;

                Point3D loc = GetRandomSpawnLocation(p, map);

                if (loc != Point3D.Zero)
                {
                    bc.MoveToWorld(p, map);
                    spawned = true;
                }

                if (!spawned)
                {
                    bc.Delete();
                    return null;
                }

                if (target != null)
                {
                    Timer.DelayCall(() => bc.Combatant = target);
                }

                return bc;
            }

            return null;
        }

        public static Point3D GetRandomSpawnLocation(Point3D p, Map map)
        {
            for (int i = 0; i < 10; ++i)
            {
                int x = p.X - 3 + Utility.Random(7);
                int y = p.Y - 3 + Utility.Random(7);

                if (map.CanSpawnMobile(x, y, p.Z))
                {
                    return new Point3D(x, y, p.Z);
                }
                else
                {
                    int z = map.GetAverageZ(x, y);

                    if (map.CanSpawnMobile(x, y, z))
                        return new Point3D(x, y, z);
                }
            }

            return Point3D.Zero;
        }

        public static bool HasDiggingTool(Mobile m)
        {
            if (m.Backpack == null)
            {
                return false;
            }

            List<BaseHarvestTool> items = m.Backpack.FindItemsByType<BaseHarvestTool>();

            for (var index = 0; index < items.Count; index++)
            {
                BaseHarvestTool tool = items[index];

                if (tool.HarvestSystem == Mining.System)
                {
                    return true;
                }
            }

            return false;
        }

        public virtual void OnBeginDig(Mobile from)
        {
            if (m_Completed)
            {
                from.SendLocalizedMessage(503028); // The treasure for this map has already been found.
            }
            else if (m_Decoder != from && !HasRequiredSkill(from))
            {
                from.SendLocalizedMessage(503031); // You did not decode this map and have no clue where to look for the treasure.
            }
            else if (!from.CanBeginAction(typeof(TreasureMap)))
            {
                from.SendLocalizedMessage(503020); // You are already digging treasure.
            }
            else if (from.Map != Facet)
            {
                from.SendLocalizedMessage(1010479); // You seem to be in the right place, but may be on the wrong facet!
            }
            else
            {
                from.SendLocalizedMessage(503033); // Where do you wish to dig?
                from.Target = new DigTarget(this);
            }
        }

        public override void OnDoubleClick(Mobile from)
        {
            if(TestCenter.Enabled)
            {
                TreasureMapChest m_Chest = new TreasureMapChest(from, this.Level, true);

                m_Chest.MoveToWorld(from.Location, from.Map);
                TreasureMapInfo.Fill(from, m_Chest, this);
                m_Chest.Movable = true;
                m_Chest.Locked = false;
                m_Chest.TrapType = TrapType.None;
                m_Chest.TrapPower = 0;
                m_Chest.TrapLevel = 0;
                m_Chest.Temporary = false;
            }

            if (!from.InRange(GetWorldLocation(), 2))
            {
                from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
                return;
            }

            if (!m_Completed && m_Decoder == null)
            {
                Decode(from);
            }
            else
            {
                DisplayTo(from);
            }
        }

        public virtual void Decode(Mobile from)
        {
            if (m_Completed || m_Decoder != null)
            {
                return;
            }

            double minSkill = GetMinSkillLevel();

            if (from.Skills[SkillName.Cartography].Value < minSkill)
            {
                if (m_Level == 1)
                {
                    from.CheckSkill(SkillName.Cartography, 0, minSkill);
                }
                else
                {
                    from.SendLocalizedMessage(503013); // The map is too difficult to attempt to decode.
                }
            }

            if (!from.CheckSkill(SkillName.Cartography, minSkill - 10, minSkill + 30))
            {
                from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 503018); // You fail to make anything of the map.
                return;
            }

            from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 503019); // You successfully decode a treasure map!
            Decoder = from;

            LootType = LootType.Blessed;

            DisplayTo(from);
        }

        public void ResetLocation()
        {
            if (!m_Completed)
            {
                ClearPins();
                LootType = LootType.Regular;
                m_Decoder = null;
                GetRandomLocation(Facet, TreasureFacet == TreasureFacet.Eodon);
                InvalidateProperties();
                NextReset = DateTime.UtcNow + ResetTime;
            }
        }

        public override void DisplayTo(Mobile from)
        {
            if (m_Completed)
            {
                SendLocalizedMessageTo(from, 503014); // This treasure hunt has already been completed.
            }
            else if (m_Decoder != from && !HasRequiredSkill(from))
            {
                from.SendLocalizedMessage(503031); // You did not decode this map and have no clue where to look for the treasure.
                return;
            }
            else
            {
                SendLocalizedMessageTo(from, 503017); // The treasure is marked by the red pin. Grab a shovel and go dig it up!
            }

            if (Pins.Count == 0)
            {
                AddWorldPin(ChestLocation.X, ChestLocation.Y);
            }

            from.PlaySound(0x249);
            base.DisplayTo(from);
        }

        public override void GetContextMenuEntries(Mobile from, List<ContextMenuEntry> list)
        {
            base.GetContextMenuEntries(from, list);

            if (!m_Completed)
            {
                if (m_Decoder == null)
                {
                    list.Add(new DecodeMapEntry(this));
                }
                else
                {
                    bool digTool = HasDiggingTool(from);

                    list.Add(new OpenMapEntry(this));
                    list.Add(new DigEntry(this, digTool));
                }
            }
        }

        public override void AddNameProperty(ObjectPropertyList list)
        {
            list.Add(m_Decoder != null ? 1158980 + (int)TreasureLevel : 1158975 + (int)TreasureLevel, "#" + TreasureMapInfo.PackageLocalization(Package));
        }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);

            TreasureFacet facet = TreasureMapInfo.GetFacet(ChestLocation, Facet);

            switch (facet)
            {
                case TreasureFacet.Trammel: list.Add(1041503); break;
                case TreasureFacet.Felucca: list.Add(1041502); break;
                case TreasureFacet.Ilshenar: list.Add(1060850); break;
                case TreasureFacet.Malas: list.Add(1060851); break;
                case TreasureFacet.Tokuno: list.Add(1115645); break;
                case TreasureFacet.TerMur: list.Add(1115646); break;
                case TreasureFacet.Eodon: list.Add(1158985); break;
            }

            if (m_Completed)
            {
                list.Add(1041507, m_CompletedBy == null ? "someone" : m_CompletedBy.Name); // completed by ~1_val~
            }
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(3);

            writer.Write((int)Package);
            writer.Write(NextReset);
            writer.Write(m_CompletedBy);
            writer.Write(m_Level);
            writer.Write(m_Completed);
            writer.Write(m_Decoder);
            writer.Write(ChestLocation);

            if (!Completed && NextReset != DateTime.MinValue && NextReset < DateTime.UtcNow)
                Timer.DelayCall(TimeSpan.FromSeconds(30), ResetLocation);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();

            Package = (TreasurePackage)reader.ReadInt();
            NextReset = reader.ReadDateTime();
            m_CompletedBy = reader.ReadMobile();
            m_Level = reader.ReadInt();
            m_Completed = reader.ReadBool();
            m_Decoder = reader.ReadMobile();
            ChestLocation = reader.ReadPoint2D();

            if (NextReset == DateTime.MinValue)
            {
                NextReset = DateTime.UtcNow + ResetTime;
            }
        }

        private double GetMinSkillLevel()
        {
            switch (m_Level)
            {
                case 0:
                    return 27;
                case 1:
                    return 70;
                case 2:
                    return 90;
                case 3:
                case 4:
                    return 100.0;

                default:
                    return 0.0;
            }
        }

        protected virtual bool HasRequiredSkill(Mobile from)
        {
            return from.Skills[SkillName.Cartography].Value >= GetMinSkillLevel();
        }

        protected class DigTarget : Target
        {
            private readonly TreasureMap m_Map;

            public DigTarget(TreasureMap map)
                : base(6, true, TargetFlags.None)
            {
                m_Map = map;
            }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (m_Map.Deleted)
                {
                    return;
                }

                Map map = m_Map.Facet;

                if (m_Map.m_Completed)
                {
                    from.SendLocalizedMessage(503028); // The treasure for this map has already been found.
                }
                else if (m_Map.m_Decoder != from && !m_Map.HasRequiredSkill(from))
                {
                    from.SendLocalizedMessage(503031); // You did not decode this map and have no clue where to look for the treasure.
                }
                else if (!from.CanBeginAction(typeof(TreasureMap)))
                {
                    from.SendLocalizedMessage(503020); // You are already digging treasure.
                }
                else if (!HasDiggingTool(from))
                {
                    from.SendLocalizedMessage(1114416); // You must have a digging tool to dig for treasure.
                }
                else if (from.Map != map)
                {
                    from.SendLocalizedMessage(1010479); // You seem to be in the right place, but may be on the wrong facet!
                }
                else
                {
                    IPoint3D p = targeted as IPoint3D;

                    Point3D targ3D;
                    if (p is Item item)
                    {
                        targ3D = item.GetWorldLocation();
                    }
                    else
                    {
                        targ3D = new Point3D(p);
                    }

                    int maxRange;
                    double skillValue = from.Skills[SkillName.Cartography].Value;

                    if (skillValue >= 100.0)
                    {
                        maxRange = 4;
                    }
                    else if (skillValue >= 81.0)
                    {
                        maxRange = 3;
                    }
                    else if (skillValue >= 51.0)
                    {
                        maxRange = 2;
                    }
                    else
                    {
                        maxRange = 1;
                    }

                    Point2D loc = m_Map.ChestLocation;
                    int x = loc.X, y = loc.Y;

                    Point3D chest3D0 = new Point3D(loc, 0);

                    if (Utility.InRange(targ3D, chest3D0, maxRange))
                    {
                        if (from.Location.X == x && from.Location.Y == y)
                        {
                            from.SendLocalizedMessage(503030); // The chest can't be dug up because you are standing on top of it.
                        }
                        else if (map != null)
                        {
                            int z = map.GetAverageZ(x, y);

                            if (!map.CanFit(x, y, z, 16, true, true))
                            {
                                from.SendLocalizedMessage(503021);
                                // You have found the treasure chest but something is keeping it from being dug up.
                            }
                            else if (from.BeginAction(typeof(TreasureMap)))
                            {
                                new DigTimer(from, m_Map, new Point3D(x, y, z), map).Start();
                            }
                            else
                            {
                                from.SendLocalizedMessage(503020); // You are already digging treasure.
                            }
                        }
                    }
                    else if (m_Map.Level > 0)
                    {
                        if (Utility.InRange(targ3D, chest3D0, 8)) // We're close, but not quite
                        {
                            from.SendLocalizedMessage(503032); // You dig and dig but no treasure seems to be here.
                        }
                        else
                        {
                            from.SendLocalizedMessage(503035); // You dig and dig but fail to find any treasure.
                        }
                    }
                    else
                    {
                        if (Utility.InRange(targ3D, chest3D0, 8)) // We're close, but not quite
                        {
                            from.SendAsciiMessage(0x44, "The treasure chest is very close!");
                        }
                        else
                        {
                            Direction dir = Utility.GetDirection(targ3D, chest3D0);

                            string sDir;
                            switch (dir)
                            {
                                case Direction.North:
                                    sDir = "north";
                                    break;
                                case Direction.Right:
                                    sDir = "northeast";
                                    break;
                                case Direction.East:
                                    sDir = "east";
                                    break;
                                case Direction.Down:
                                    sDir = "southeast";
                                    break;
                                case Direction.South:
                                    sDir = "south";
                                    break;
                                case Direction.Left:
                                    sDir = "southwest";
                                    break;
                                case Direction.West:
                                    sDir = "west";
                                    break;
                                default:
                                    sDir = "northwest";
                                    break;
                            }

                            from.SendAsciiMessage(0x44, "Try looking for the treasure chest more to the {0}.", sDir);
                        }
                    }
                }
            }
        }

        private class DigTimer : Timer
        {
            private readonly Mobile m_From;
            private readonly TreasureMap m_TreasureMap;
            private readonly Map m_Map;
            private readonly long m_NextSkillTime;
            private readonly long m_NextActionTime;
            private readonly long m_LastMoveTime;
            private TreasureChestDirt m_Dirt1;
            private TreasureChestDirt m_Dirt2;
            private TreasureMapChest m_Chest;
            private int m_Count;

            public Point3D ChestLocation { get; }

            public DigTimer(Mobile from, TreasureMap treasureMap, Point3D location, Map map)
                : base(TimeSpan.Zero, TimeSpan.FromSeconds(1.0))
            {
                m_From = from;
                m_TreasureMap = treasureMap;

                ChestLocation = location;
                m_Map = map;

                m_NextSkillTime = from.NextSkillTime;
                m_NextActionTime = from.NextActionTime;
                m_LastMoveTime = from.LastMoveTime;

                Priority = TimerPriority.TenMS;
            }            

            protected override void OnTick()
            {
                if (m_NextSkillTime != m_From.NextSkillTime || m_NextActionTime != m_From.NextActionTime)
                {
                    Terminate();
                    return;
                }

                if (m_LastMoveTime != m_From.LastMoveTime)
                {
                    m_From.SendLocalizedMessage(503023); // You cannot move around while digging up treasure. You will need to start digging anew.
                    Terminate();
                    return;
                }

                int z = m_Chest != null ? m_Chest.Z + m_Chest.ItemData.Height : int.MinValue;
                int height = 16;

                if (z > ChestLocation.Z)
                {
                    height -= z - ChestLocation.Z;
                }
                else
                {
                    z = ChestLocation.Z;
                }

                if (!m_Map.CanFit(ChestLocation.X, ChestLocation.Y, z, height, true, true, false))
                {
                    m_From.SendLocalizedMessage(503024); // You stop digging because something is directly on top of the treasure chest.
                    Terminate();
                    return;
                }

                m_Count++;

                m_From.RevealingAction();
                m_From.Direction = m_From.GetDirectionTo(ChestLocation);

                if (m_Count > 1 && m_Dirt1 == null)
                {
                    m_Dirt1 = new TreasureChestDirt();
                    m_Dirt1.MoveToWorld(ChestLocation, m_Map);

                    m_Dirt2 = new TreasureChestDirt();
                    m_Dirt2.MoveToWorld(new Point3D(ChestLocation.X, ChestLocation.Y - 1, ChestLocation.Z), m_Map);
                }

                if (m_Count == 5)
                {
                    m_Dirt1.Turn1();
                }
                else if (m_Count == 10)
                {
                    m_Dirt1.Turn2();
                    m_Dirt2.Turn2();
                }
                else if (m_Count > 10)
                {
                    if (m_Chest == null)
                    {
                        m_Chest = new TreasureMapChest(m_From, m_TreasureMap.Level, true);

                        m_TreasureMap.AssignChestQuality(m_From, m_Chest);

                        m_Chest.MoveToWorld(new Point3D(ChestLocation.X, ChestLocation.Y, ChestLocation.Z - 15), m_Map);
                    }
                    else
                    {
                        m_Chest.Z++;
                    }

                    Effects.PlaySound(m_Chest, m_Map, 0x33B);
                }

                if (m_Chest != null && m_Chest.Location.Z >= ChestLocation.Z)
                {
                    Stop();
                    m_From.EndAction(typeof(TreasureMap));

                    m_Chest.Temporary = false;
                    m_Chest.TreasureMap = m_TreasureMap;
                    m_Chest.DigTime = DateTime.UtcNow;
                    m_TreasureMap.Completed = true;
                    m_TreasureMap.CompletedBy = m_From;

                    TreasureMapInfo.Fill(m_From, m_Chest, m_TreasureMap);

                    m_TreasureMap.OnMapComplete(m_From, m_Chest);

                    int spawns = Utility.RandomMinMax(4, 8);

                    for (int i = 0; i < spawns; ++i)
                    {
                        bool guardian = Utility.RandomDouble() >= 0.3;

                        BaseCreature bc = Spawn(m_TreasureMap.Level, m_Chest.Location, m_Chest.Map, null, guardian);

                        if (bc != null && guardian && !IsTameable(bc))
                        {
                            m_Chest.Guardians.Add(bc);                            
                        }
                    }

                    new ReturnToHomeTimer(m_Chest).Start();
                }
                else
                {
                    if (m_From.Body.IsHuman && !m_From.Mounted)
                    {
                        m_From.Animate(AnimationType.Attack, 3);
                    }

                    new SoundTimer(m_From, 0x125 + m_Count % 2).Start();
                }
            }

            private void Terminate()
            {
                Stop();
                m_From.EndAction(typeof(TreasureMap));

                m_Chest?.Delete();

                if (m_Dirt1 != null)
                {
                    m_Dirt1.Delete();
                    m_Dirt2.Delete();
                }
            }

            private class ReturnToHomeTimer : Timer
            {
                private readonly TreasureMapChest m_Chest;

                public ReturnToHomeTimer(TreasureMapChest chest)
                    : base(TimeSpan.FromSeconds(5.0), TimeSpan.FromSeconds(5.0))
                {
                    m_Chest = chest;
                }

                protected override void OnTick()
                {
                    if (m_Chest.Deleted || m_Chest.Guardians.Count == 0)
                    {
                        Stop();
                    }
                    else
                    {
                        for (var index = 0; index < m_Chest.Guardians.Count; index++)
                        {
                            var bc = m_Chest.Guardians[index];

                            if (!bc.InRange(m_Chest, 25))
                            {
                                ReturnToHome(bc);
                            }
                        }
                    }
                }

                private void ReturnToHome(Mobile m)
                {
                    var loc = GetRandomSpawnLocation(m_Chest.Location, m_Chest.Map);

                    if (loc != Point3D.Zero)
                    {
                        m.MoveToWorld(loc, m_Chest.Map);
                    }
                }
            }

            private class SoundTimer : Timer
            {
                private readonly Mobile m_From;
                private readonly int m_SoundID;

                public SoundTimer(Mobile from, int soundID)
                    : base(TimeSpan.FromSeconds(0.9))
                {
                    m_From = from;
                    m_SoundID = soundID;

                    Priority = TimerPriority.TenMS;
                }

                protected override void OnTick()
                {
                    m_From.PlaySound(m_SoundID);
                }
            }
        }

        private class DecodeMapEntry : ContextMenuEntry
        {
            private readonly TreasureMap m_Map;

            public DecodeMapEntry(TreasureMap map)
                : base(6147, 2)
            {
                m_Map = map;
            }

            public override void OnClick()
            {
                if (!m_Map.Deleted)
                {
                    m_Map.Decode(Owner.From);
                }
            }
        }

        private class OpenMapEntry : ContextMenuEntry
        {
            private readonly TreasureMap m_Map;

            public OpenMapEntry(TreasureMap map)
                : base(6150, 2)
            {
                m_Map = map;
            }

            public override void OnClick()
            {
                if (!m_Map.Deleted)
                {
                    m_Map.DisplayTo(Owner.From);
                }
            }
        }

        private class DigEntry : ContextMenuEntry
        {
            private readonly TreasureMap m_Map;

            public DigEntry(TreasureMap map, bool enabled)
                : base(6148, 2)
            {
                m_Map = map;

                if (!enabled)
                {
                    Flags |= CMEFlags.Disabled;
                }
            }

            public override void OnClick()
            {
                if (m_Map.Deleted)
                {
                    return;
                }

                Mobile from = Owner.From;

                if (HasDiggingTool(from))
                {
                    m_Map.OnBeginDig(from);
                }
                else
                {
                    from.SendLocalizedMessage(1114416); // You must have a digging tool to dig for treasure.
                }
            }
        }
    }

    public class TreasureChestDirt : Item
    {
        public TreasureChestDirt()
            : base(0x912)
        {
            Movable = false;

            Timer.DelayCall(TimeSpan.FromMinutes(2.0), Delete);
        }

        public TreasureChestDirt(Serial serial)
            : base(serial)
        { }

        public void Turn1()
        {
            ItemID = 0x913;
        }

        public void Turn2()
        {
            ItemID = 0x914;
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.WriteEncodedInt(0); // version
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadEncodedInt();

            Delete();
        }
    }
}
