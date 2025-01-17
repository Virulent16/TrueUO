using Server.Commands;
using Server.Items;
using Server.Mobiles;
using System;
using System.Collections.Generic;

namespace Server.Engines.Despise
{
    public class DespiseController : Item
    {
        public static void Initialize()
        {
            EventSink.Login += OnLogin;
            EventSink.OnEnterRegion += OnEnterRegion;

            if (m_Instance != null)
                CommandSystem.Register("CheckSpawnersVersion3", AccessLevel.Administrator, m_Instance.CheckSpawnersVersion3);
        }

        private static DespiseController m_Instance;
        public static DespiseController Instance { get => m_Instance; set => m_Instance = value; }

        private bool m_Enabled;
        private bool m_Sequencing;
        private DateTime m_NextBossEncounter;
        private DespiseBoss m_Boss;
        private DateTime m_DeadLine;
        private Alignment m_SequenceAlignment;
        private bool m_PlayersInSequence;

        private Timer m_Timer;
        private Timer m_SequenceTimer;
        private Timer m_CleanupTimer;

        private DespiseRegion m_GoodRegion;
        private DespiseRegion m_EvilRegion;
        private DespiseRegion m_LowerRegion;
        private DespiseRegion m_StartRegion;

        public Region GoodRegion => m_GoodRegion;
        public Region EvilRegion => m_EvilRegion;
        public Region LowerRegion => m_LowerRegion;
        public Region StartRegion => m_StartRegion;

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Enabled
        {
            get => m_Enabled;
            set
            {
                if (m_Enabled != value)
                {
                    m_Enabled = value;

                    if (m_Enabled)
                        BeginTimer();
                    else
                        EndTimer();
                }
            }
        }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Sequencing => m_Sequencing;

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime NextBossEncounter { get => m_NextBossEncounter; set => m_NextBossEncounter = value; }

        [CommandProperty(AccessLevel.GameMaster)]
        public DespiseBoss Boss => m_Boss;

        [CommandProperty(AccessLevel.GameMaster)]
        public DateTime DeadLine => m_DeadLine;

        [CommandProperty(AccessLevel.GameMaster)]
        public Alignment SequenceAlignment => m_SequenceAlignment;

        private readonly List<DespiseCreature> m_EvilArmy = new List<DespiseCreature>();
        private readonly List<DespiseCreature> m_GoodArmy = new List<DespiseCreature>();

        public List<DespiseCreature> EvilArmy => m_EvilArmy;
        public List<DespiseCreature> GoodArmy => m_GoodArmy;

        private readonly List<Mobile> m_ToTransport = new List<Mobile>();

        private readonly TimeSpan EncounterCheckDuration = TimeSpan.FromMinutes(5);
        private readonly TimeSpan DeadLineDuration = TimeSpan.FromMinutes(90);

        public bool IsInSequence => m_SequenceTimer != null || m_CleanupTimer != null;

        public DespiseController()
            : base(3806)
        {
            Movable = false;
            Visible = false;

            m_Enabled = true;
            m_Instance = this;

            m_NextBossEncounter = DateTime.UtcNow;
            m_Boss = null;

            if (m_Enabled)
                BeginTimer();

            CreateSpawners();
        }

        public static WispOrb GetWispOrb(Mobile from)
        {
            for (var index = 0; index < WispOrb.Orbs.Count; index++)
            {
                WispOrb orb = WispOrb.Orbs[index];

                if (orb != null && !orb.Deleted && orb.Owner == from)
                {
                    return orb;
                }
            }

            return null;
        }

        private void BeginTimer()
        {
            EndTimer();

            m_Timer = Timer.DelayCall(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), OnTick);

            m_LowerRegion = new DespiseRegion("Despise Lower", m_LowerLevelBounds, true);
            m_EvilRegion = new DespiseRegion("Despise Evil", m_EvilBounds);
            m_GoodRegion = new DespiseRegion("Despise Good", m_GoodBounds);
            m_StartRegion = new DespiseRegion("Despise Start", new[] { new Rectangle2D(5568, 623, 22, 20) });
        }

        private void EndTimer()
        {
            if (m_Timer != null)
            {
                m_Timer.Stop();
                m_Timer = null;
            }

            m_LowerRegion?.Unregister();
            m_EvilRegion?.Unregister();
            m_GoodRegion?.Unregister();
            m_StartRegion?.Unregister();

            m_LowerRegion = null;
            m_EvilRegion = null;
            m_GoodRegion = null;
            m_StartRegion = null;
        }

        private void OnTick()
        {
            if (m_NextBossEncounter == DateTime.MinValue || m_NextBossEncounter > DateTime.UtcNow)
                return;

            int good = GetArmyPower(Alignment.Good);
            int evil = GetArmyPower(Alignment.Evil);
            Alignment strongest = Alignment.Neutral;

            if (good == 0 && evil == 0)
            {
                m_NextBossEncounter = DateTime.UtcNow + EncounterCheckDuration;
            }
            else
            {
                if (good > evil) strongest = Alignment.Good;
                else if (good < evil) strongest = Alignment.Evil;
                else strongest = 0.5 > Utility.RandomDouble() ? Alignment.Good : Alignment.Evil;
            }

            List<Mobile> players = new List<Mobile>();
            players.AddRange(m_GoodRegion.GetPlayers());
            players.AddRange(m_EvilRegion.GetPlayers());
            players.AddRange(m_StartRegion.GetPlayers());

            for (var index = 0; index < players.Count; index++)
            {
                Mobile m = players[index];

                if (!m.Player)
                {
                    continue;
                }

                WispOrb orb = GetWispOrb(m);
                m.PlaySound(0x66C);

                if (orb == null || orb.Alignment != strongest)
                {
                    m.SendLocalizedMessage(strongest != Alignment.Neutral ? 1153334 : 1153333);
                    // The Call to Arms has sounded, but your forces are not yet strong enough to heed it.
                    // Your enemy forces are stronger, and they have been called to battle.
                }
                else if (orb.Alignment == strongest)
                {
                    m.SendLocalizedMessage(
                        1153332); // The Call to Arms has sounded. The forces of your alignment are strong, and you have been called to battle!

                    if (orb.Conscripted)
                    {
                        m.SendLocalizedMessage(
                            1153337); // You will be teleported into the depths of the dungeon within 60 seconds to heed the Call to Arms, unless you release your conscripted creature or it dies.
                        m_ToTransport.Add(m);
                    }
                    else
                        m.SendLocalizedMessage(
                            1153338); // You have under 60 seconds to conscript a creature to answer the Call to Arms, or you will not be summoned for the battle.
                }
            }

            if (strongest != Alignment.Neutral)
            {
                ColUtility.Free(players);
                m_SequenceAlignment = strongest;

                Timer.DelayCall(TimeSpan.FromSeconds(60), BeginSequence);
                m_NextBossEncounter = DateTime.MinValue;
                m_Sequencing = true;
            }
        }

        private static int GetArmyPower(Alignment alignment)
        {
            int power = 0;

            for (var index = 0; index < WispOrb.Orbs.Count; index++)
            {
                WispOrb orb = WispOrb.Orbs[index];

                if (orb.Conscripted && orb.Alignment == alignment)
                {
                    power += orb.GetArmyPower();
                }
            }

            return power;
        }

        public void TryAddToArmy(WispOrb orb)
        {
            if (orb != null && orb.Owner != null && m_Sequencing && orb.Alignment == m_SequenceAlignment && !m_ToTransport.Contains(orb.Owner))
                m_ToTransport.Add(orb.Owner);
        }

        #region Spawner Stuff
        private List<XmlSpawner> m_GoodSpawners;
        private List<XmlSpawner> m_EvilSpawners;

        [CommandProperty(AccessLevel.GameMaster)]
        public int GoodSpawnerCount => m_GoodSpawners == null ? 0 : m_GoodSpawners.Count;

        [CommandProperty(AccessLevel.GameMaster)]
        public int EvilSpawnerCount => m_EvilSpawners == null ? 0 : m_EvilSpawners.Count;

        [CommandProperty(AccessLevel.GameMaster)]
        public bool ResetSpawns
        {
            get => true;
            set
            {
                if (value)
                {
                    if (m_GoodSpawners != null) m_GoodSpawners.Clear();
                    if (m_EvilSpawners != null) m_EvilSpawners.Clear();

                    CreateSpawners();
                }
            }
        }

        private void CreateSpawners()
        {
            m_GoodSpawners = new List<XmlSpawner>();
            m_EvilSpawners = new List<XmlSpawner>();

            foreach (Item item in m_LowerRegion.GetEnumeratedItems())
            {
                if (item is XmlSpawner spawner && spawner.Name != null && spawner.Name.ToLower().IndexOf("despiserevamped") >= 0)
                {
                    if (spawner.Name.ToLower().IndexOf("despiserevamped good") >= 0)
                        m_GoodSpawners.Add(spawner);
                    if (spawner.Name.ToLower().IndexOf("despiserevamped evil") >= 0)
                        m_EvilSpawners.Add(spawner);
                }
            }
        }

        private void ResetSpawners(bool reset)
        {
            if (reset)
            {
                for (var index = 0; index < m_EvilSpawners.Count; index++)
                {
                    XmlSpawner spawner = m_EvilSpawners[index];

                    if (spawner.Running)
                    {
                        spawner.DoReset = true;
                    }
                }

                for (var index = 0; index < m_GoodSpawners.Count; index++)
                {
                    XmlSpawner spawner = m_GoodSpawners[index];

                    if (spawner.Running)
                    {
                        spawner.DoReset = true;
                    }
                }
            }
            else
            {
                List<XmlSpawner> useList;

                if (m_SequenceAlignment == Alignment.Good)
                {
                    useList = m_EvilSpawners;
                }
                else
                {
                    useList = m_GoodSpawners;
                }

                if (useList == null)
                {
                    return;
                }

                for (var index = 0; index < useList.Count; index++)
                {
                    XmlSpawner spawner = useList[index];

                    spawner.DoRespawn = true;
                }

                ColUtility.Free(useList);
            }
        }
        #endregion

        #region Instance Sequence

        private void BeginSequence()
        {
            m_Sequencing = false;

            if (m_ToTransport.Count == 0)
            {
                m_NextBossEncounter = DateTime.UtcNow + EncounterCheckDuration;
                m_SequenceAlignment = Alignment.Neutral;
                return;
            }

            if (m_SequenceAlignment == Alignment.Good)
                m_Boss = new AndrosTheDreadLord();
            else
                m_Boss = new AdrianTheGloriousLord();

            ResetSpawners(false);

            m_Boss.MoveToWorld(BossLocation, Map.Trammel);
            m_DeadLine = DateTime.UtcNow + DeadLineDuration;

            BeginSequenceTimer();
            KickFromBossRegion(false);

            Timer.DelayCall(TimeSpan.FromSeconds(60), TransportPlayers);

            Timer.DelayCall(TimeSpan.FromSeconds(12), new TimerStateCallback(SendReadyMessage_Callback), 1153339); // You have been called to assist in a fight of good versus evil. Fight your way to the Lake, and defeat the enemy overlord and its lieutenants!
            Timer.DelayCall(TimeSpan.FromSeconds(24), new TimerStateCallback(SendReadyMessage_Callback), 1153340); // The Overlord is shielded from all attacks by players, but not by creatures possessed by Wisp Orbs. You must protect your controlled creature as it fights.
            Timer.DelayCall(TimeSpan.FromSeconds(36), new TimerStateCallback(SendReadyMessage_Callback), 1153341); // The Lieutenants are vulnerable to your attacks. If you die during this battle, your possessed creature will fall. Furthermore, your ghost and your corpse will be teleported back to your home base.
        }

        private void EndSequence()
        {
            if (m_Boss != null && !m_Boss.Deleted)
                m_Boss.Delete();

            m_Boss = null;
            m_PlayersInSequence = false;
            EndCleanupTimer();
            KickFromBossRegion(false);
            m_SequenceAlignment = Alignment.Neutral;

            m_DeadLine = DateTime.MinValue;
            m_ToTransport.Clear();

            Timer.DelayCall(TimeSpan.FromSeconds(10), () => ResetSpawners(true));

            m_NextBossEncounter = DateTime.UtcNow + EncounterCheckDuration;
        }

        private void OnSequenceTick()
        {
            if (m_SequenceTimer != null && m_DeadLine < DateTime.UtcNow && m_LowerRegion != null)
            {
                EndSequenceTimer();
                SendRegionMessage(m_LowerRegion, 1153348); // You were unable to defeat the enemy overlord in the time allotted. He has activated a Doom Spell!

                Timer.DelayCall(TimeSpan.FromSeconds(1), EndSequence);
            }
            else if (m_PlayersInSequence && !HasPlayers(m_LowerRegion))
            {
                EndSequenceTimer();
                Timer.DelayCall(TimeSpan.FromSeconds(1), EndSequence);
            }
        }

        public void OnBossSlain()
        {
            EndSequenceTimer();
            SendRegionMessage(m_LowerRegion, 1153343); // The battle has ended. The battlefield will be cleared in five minutes and you will be returned to your home base at that time.

            BeginCleanupTimer();
        }

        private static void SendRegionMessage(Region region, int cliloc)
        {
            if (region != null)
            {
                foreach (Mobile m in region.GetEnumeratedMobiles())
                {
                    if (m is PlayerMobile)
                    {
                        m.SendLocalizedMessage(cliloc);
                    }
                }
            }
        }

        private void KickFromBossRegion(bool deletepet)
        {
            if (m_LowerRegion == null)
                return;

            List<Mobile> mobiles = m_LowerRegion.GetPlayers();
            Rectangle2D bounds = m_SequenceAlignment == Alignment.Evil ? EvilKickBounds : GoodKickBounds;

            for (var index = 0; index < mobiles.Count; index++)
            {
                Mobile m = mobiles[index];

                WispOrb orb = GetWispOrb(m);
                Point3D p = GetRandomLoc(bounds);

                m.MoveToWorld(p, Map.Trammel);

                if (orb != null && deletepet)
                {
                    if (orb.Pet != null)
                    {
                        orb.Pet.Delete();
                        orb.Pet = null;
                    }

                    orb.Delete();
                    m.SendLocalizedMessage(1153312); // The Wisp Orb dissolves into aether.
                }
                else if (orb != null && orb.Pet != null && orb.Pet.Alive)
                {
                    orb.Pet.MoveToWorld(p, Map.Trammel);
                }

                m.SendLocalizedMessage(1153346); // You are summoned back to your stronghold.
            }

            ColUtility.Free(mobiles);
        }

        private void TransportPlayers()
        {
            List<Mobile> list = new List<Mobile>(m_ToTransport);

            for (var index = 0; index < list.Count; index++)
            {
                Mobile m = list[index];
                WispOrb orb = GetWispOrb(m);

                if (orb == null || orb.Deleted || !orb.Conscripted || m.Region == null || !m.Region.IsPartOf<DespiseRegion>())
                {
                    m_ToTransport.Remove(m);
                }
            }

            if (m_ToTransport.Count == 0)
            {
                EndSequenceTimer();
                EndSequence();
            }
            else
            {
                for (var index = 0; index < m_ToTransport.Count; index++)
                {
                    Mobile m = m_ToTransport[index];

                    if (m != null && m.Region != null && m.Region.IsPartOf<DespiseRegion>())
                    {
                        WispOrb orb = GetWispOrb(m);

                        if (orb != null && orb.Pet != null && orb.Pet.Alive)
                        {
                            Point3D p = GetRandomLoc(BossEntranceLocation);
                            m.MoveToWorld(p, Map.Trammel);
                            orb.Pet.MoveToWorld(p, Map.Trammel);

                            m.SendLocalizedMessage(1153280, "You!");
                            orb.Anchor = m;
                            orb.Pet.FollowTarget = m;
                            orb.Pet.ControlOrder = LastOrderType.Follow;
                        }
                    }
                }

                m_PlayersInSequence = true;
            }
        }

        private static bool HasPlayers(Region r)
        {
            return r != null && r.GetPlayerCount() > 0;
        }

        private static Point3D GetRandomLoc(Rectangle2D rec)
        {
            Map map = Map.Trammel;
            Point3D p = new Point3D(rec.X, rec.Y, map.GetAverageZ(rec.X, rec.Y));

            for (int i = 0; i < 50; i++)
            {
                int x = Utility.RandomMinMax(rec.X, rec.X + rec.Width);
                int y = Utility.RandomMinMax(rec.Y, rec.Y + rec.Height);
                int z = map.GetAverageZ(x, y);

                if (map.CanSpawnMobile(x, y, z))
                {
                    p = new Point3D(x, y, z);
                    break;
                }
            }

            return p;
        }

        public void BeginSequenceTimer()
        {
            EndSequenceTimer();

            m_SequenceTimer = Timer.DelayCall(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), OnSequenceTick);
            m_SequenceTimer.Start();
        }

        public void EndSequenceTimer()
        {
            if (m_SequenceTimer != null)
            {
                m_SequenceTimer.Stop();
                m_SequenceTimer = null;
            }
        }

        public void BeginCleanupTimer()
        {
            EndCleanupTimer();
            m_CleanupTimer = Timer.DelayCall(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5), EndSequence);
            m_CleanupTimer.Start();

            var ms = m_LowerRegion.GetMobiles();

            for (var index = 0; index < ms.Count; index++)
            {
                Mobile m = ms[index];

                if (m is DespiseCreature creature && creature.Orb != null)
                {
                    creature.Delete();
                }
            }
        }

        public void EndCleanupTimer()
        {
            if (m_CleanupTimer != null)
            {
                m_CleanupTimer.Stop();
                m_CleanupTimer = null;
            }
        }

        public static void OnLogin(LoginEventArgs e)
        {
            Mobile from = e.Mobile;

            DespiseController controller = m_Instance;

            if (controller != null && controller.LowerRegion != null)
            {
                if (from.Region != null && from.Region.IsPartOf(controller.LowerRegion) && !controller.IsInSequence)
                {
                    WispOrb orb = GetWispOrb(from);
                    Rectangle2D bounds = EvilKickBounds;

                    if (orb != null && orb.Alignment == Alignment.Good)
                        bounds = GoodKickBounds;

                    while (true)
                    {
                        int x = Utility.RandomMinMax(bounds.X, bounds.X + bounds.Width);
                        int y = Utility.RandomMinMax(bounds.Y, bounds.Y + bounds.Height);
                        int z = Map.Trammel.GetAverageZ(x, y);

                        if (Map.Trammel.CanSpawnMobile(x, y, z))
                        {
                            from.MoveToWorld(new Point3D(x, y, z), Map.Trammel);
                            if (orb != null && orb.Pet != null && orb.Pet.Alive)
                                orb.Pet.MoveToWorld(new Point3D(x, y, z), Map.Trammel);
                            break;
                        }
                    }
                }
            }
        }

        public static void OnEnterRegion(OnEnterRegionEventArgs e)
        {
            WispOrb orb = GetWispOrb(e.From);

            if (orb != null && !Region.Find(e.From.Location, e.From.Map).IsPartOf<DespiseRegion>())
            {
                Timer.DelayCall(() =>
                    {
                        e.From.SendLocalizedMessage(1153233); // The Wisp Orb vanishes to whence it came...
                        orb.Delete();
                    });
            }
        }

        private void SendReadyMessage_Callback(object o)
        {
            int cliloc = (int)o;

            for (var index = 0; index < m_ToTransport.Count; index++)
            {
                Mobile m = m_ToTransport[index];

                m.SendLocalizedMessage(cliloc);
            }
        }

        #endregion

        #region Location Defs

        public static Rectangle2D[] EvilBounds => m_EvilBounds;
        private static readonly Rectangle2D[] m_EvilBounds =
        {
            new Rectangle2D(5381, 644, 149, 120)
        };

        public static Rectangle2D[] GoodBounds => m_GoodBounds;
        private static readonly Rectangle2D[] m_GoodBounds =
        {
            new Rectangle2D(5380, 515, 134, 121)
        };

        public static Rectangle2D[] LowerLevelBounds => m_LowerLevelBounds;
        private static readonly Rectangle2D[] m_LowerLevelBounds =
        {
            new Rectangle2D(5379, 771, 247, 250)
        };

        private static readonly Rectangle2D EvilKickBounds = new Rectangle2D(5500, 571, 20, 5);
        private static readonly Rectangle2D GoodKickBounds = new Rectangle2D(5484, 567, 15, 8);
        private static readonly Rectangle2D BossEntranceLocation = new Rectangle2D(5391, 855, 13, 15);

        private static readonly Point3D BossLocation = new Point3D(5556, 823, 45);

        #endregion

        public static void RemoveAnkh()
        {
            IPooledEnumerable eable = Map.Trammel.GetItemsInRange(new Point3D(5474, 525, 79), 3);

            foreach (Item item in eable)
            {
                if (item is RejuvinationAddonComponent && !item.Deleted)
                {
                    item.Delete();
                }
            }
        }

        public DespiseController(Serial serial)
            : base(serial)
        {
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(5);

            writer.Write(m_Enabled);
            writer.Write(m_NextBossEncounter);
            writer.Write(m_Boss);
            writer.Write(m_DeadLine);
            writer.Write((int)m_SequenceAlignment);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            m_EvilSpawners = new List<XmlSpawner>();
            m_GoodSpawners = new List<XmlSpawner>();

            m_Instance = this;
            m_Enabled = reader.ReadBool();
            m_NextBossEncounter = reader.ReadDateTime();
            m_Boss = reader.ReadMobile() as DespiseBoss;
            m_DeadLine = reader.ReadDateTime();
            m_SequenceAlignment = (Alignment)reader.ReadInt();

            if (version < 4)
                Timer.DelayCall(TimeSpan.FromSeconds(30), CheckSpawnersVersion3);

            if (version < 5)
            {
                int count = reader.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    reader.ReadItem();
                }

                count = reader.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    reader.ReadItem();
                }
            }

            Timer.DelayCall(CreateSpawners);

            //Conversion to new Point System
            if (version == 0)
            {
                int count = reader.ReadInt();
                for (int i = 0; i < count; i++)
                {
                    Mobile m = reader.ReadMobile();
                    int points = reader.ReadInt();

                    if (m != null && points > 0)
                        Points.PointsSystem.DespiseCrystals.ConvertFromOldSystem((PlayerMobile)m, points);
                }
            }

            if (!m_Enabled)
                return;

            BeginTimer();

            if (m_DeadLine > DateTime.UtcNow)
            {
                if (m_Boss != null && m_Boss.Alive)
                {
                    BeginSequenceTimer();
                    return;
                }
            }
            else if (m_DeadLine != DateTime.MinValue)
            {
                BeginCleanupTimer();
                return;
            }

            Timer.DelayCall(EndSequence);

            if (version < 2)
                Timer.DelayCall(TimeSpan.FromSeconds(30), RemoveAnkh);
        }

        public void CheckSpawnersVersion3(CommandEventArgs e)
        {
            CheckSpawnersVersion3();
        }

        public void CheckSpawnersVersion3()
        {
            foreach (Item value in World.Items.Values)
            {
                if (value is XmlSpawner spawner && spawner.Name != null && spawner.Name.ToLower().IndexOf("despiserevamped") >= 0)
                {
                    for (var index = 0; index < spawner.SpawnObjects.Length; index++)
                    {
                        XmlSpawner.SpawnObject obj = spawner.SpawnObjects[index];

                        if (obj.TypeName != null)
                        {
                            if (obj.TypeName.ToLower().IndexOf("berlingblades") >= 0)
                            {
                                string name = obj.TypeName;

                                obj.TypeName = name.Replace("BerlingBlades", "BirlingBlades");
                            }
                            else if (obj.TypeName.ToLower().IndexOf("sagittari") >= 0)
                            {
                                string name = obj.TypeName;

                                obj.TypeName = name.Replace("Sagittari", "Sagittarri");
                            }
                        }

                        if (obj.TypeName != null && (Region.Find(spawner.Location, spawner.Map) == m_GoodRegion || Region.Find(spawner.Location, spawner.Map) == m_EvilRegion) && obj.TypeName.IndexOf(",{RND,1,5}") < 0)
                        {
                            obj.TypeName = obj.TypeName + ",{RND,1,5}";
                        }
                    }
                }
            }

            for (var index = 0; index < new Region[] {m_GoodRegion, m_EvilRegion, m_LowerRegion, m_StartRegion}.Length; index++)
            {
                Region r = new Region[] {m_GoodRegion, m_EvilRegion, m_LowerRegion, m_StartRegion}[index];

                foreach (Item item in r.GetEnumeratedItems())
                {
                    if (item is Moongate || item is GateTeleporter)
                    {
                        item.Delete();
                        WeakEntityCollection.Remove("despise", item);
                    }
                }
            }

            DespiseRevampedSetup.SetupTeleporters();
        }
    }
}
