#region References
using System;
using System.Collections.Generic;
using System.IO;
#endregion

namespace Server.Engines.Quests
{
    public class QuestWriter
    {
        public static int Version(GenericWriter writer, int version)
        {
            if (writer == null)
            {
                return -1;
            }

            writer.Write(0x7FFFFFFF); // Preamble

            writer.Write(version);

            return version;
        }

        public static bool SubWrite(GenericWriter writer, Action<GenericWriter> serializer)
        {
            if (writer == null)
            {
                return false;
            }

            using (MemoryStream s = new MemoryStream())
            {
                BinaryFileWriter w = new BinaryFileWriter(s, true);

                try
                {
                    serializer(w);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Quest Save Failure: {0}", Utility.FormatDelegate(serializer));
                    Diagnostics.ExceptionLogging.LogException(e);

                    writer.Write(0L);

                    return false;
                }
                finally
                {
                    w.Flush();
                }

                writer.Write(s.Length);

                s.Position = 0;

                while (s.Position < s.Length)
                {
                    writer.Write((byte)s.ReadByte());
                }
            }

            return true;
        }

        public static void Quests(GenericWriter writer, List<BaseQuest> quests)
        {
            if (writer == null)
            {
                return;
            }

            Version(writer, 0);

            if (quests == null)
            {
                writer.Write(0);
                return;
            }

            writer.Write(quests.Count);

            for (var index = 0; index < quests.Count; index++)
            {
                BaseQuest quest = quests[index];

                Type(writer, quest.GetType());

                SubWrite(writer, quest.Serialize);
            }
        }

        public static void Chains(GenericWriter writer, Dictionary<QuestChain, BaseChain> chains)
        {
            if (writer == null)
            {
                return;
            }

            Version(writer, 0);

            if (chains == null)
            {
                writer.Write(0);
                return;
            }

            writer.Write(chains.Count);

            foreach (KeyValuePair<QuestChain, BaseChain> pair in chains)
            {
                writer.Write((int)pair.Key);

                Type(writer, pair.Value.CurrentQuest);
                Type(writer, pair.Value.Quester);
            }
        }

        public static void Object(GenericWriter writer, object obj)
        {
            if (writer == null)
            {
                return;
            }

            Version(writer, 0);

            if (obj is int i)
            {
                writer.Write((byte)0x1);
                writer.Write(i);
            }
            else if (obj is string s)
            {
                writer.Write((byte)0x2);
                writer.Write(s);
            }
            else if (obj is Item item)
            {
                writer.Write((byte)0x3);
                writer.Write(item);
            }
            else if (obj is Mobile mobile)
            {
                writer.Write((byte)0x4);
                writer.Write(mobile);
            }
            else
            {
                writer.Write((byte)0x0); // invalid
            }
        }

        public static void Type(GenericWriter writer, Type type)
        {
            if (writer != null)
            {
                writer.Write(type == null ? null : type.FullName);
            }
        }
    }
}
