using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoteConverter
{
    public static class Converter
    {
        private static readonly string PAGE_SEP = "\n   ";
        private static readonly string BOOK_SEP = "\n\n";


        public static List<List<string>> Convert(string NbsFilePath) // TODO split into several functions
        {
            if (!NbsFilePath.EndsWith(".nbs"))
                throw new Exception("Файл должен иметь расширение \".nbs\"");

            List<List<string>> books = new List<List<string>>();
            FileStream inStream = new FileStream(NbsFilePath, FileMode.Open);
            using (BinaryReader reader = new BinaryReader(inStream))
            {
                byte[] isOld = reader.ReadBytes(2);
                if (BitConverter.ToUInt16(isOld, 0) != 0)
                    throw new Exception("Устаревший формат [Old format]");
                byte[] version = reader.ReadBytes(1);
                byte[] vanillaInstCount = reader.ReadBytes(1);
                byte[] songLength = reader.ReadBytes(2);
                byte[] layersCount = reader.ReadBytes(2);

                int nameLen = reader.ReadInt32();
                byte[] name = reader.ReadBytes(nameLen);
                int authorLen = reader.ReadInt32();
                byte[] author = reader.ReadBytes(authorLen);
                int origAuthorLen = reader.ReadInt32();
                byte[] origAuthor = reader.ReadBytes(origAuthorLen);
                int descriptionLen = reader.ReadInt32();
                byte[] description = reader.ReadBytes(descriptionLen);

                int TICKRATE = reader.ReadInt16();
                if (TICKRATE <= 0 || TICKRATE % 100 != 0 || 2000 % TICKRATE != 0)
                    throw new Exception("Неподходящий тикрейт, 20 должно нацело делиться на " + (TICKRATE / 100.0));
                reader.ReadBytes(1 * 3 + 4 * 5); // auto-saving x2, time signature, work data
                int midiSchematicNameLen = reader.ReadInt32();
                reader.ReadBytes(midiSchematicNameLen);
                reader.ReadBytes(1 * 2 + 2 * 1); // loop data

                // end of part 1

                string setting_nbs = "nbs " + (TICKRATE / 100) + ',';
                //output.Write("имя=" + Encoding.UTF8.GetString(name) + ',');
                List<Layer> LAYERS = new List<Layer>();
                int tick = -1;
                int ticks = reader.ReadInt16();
                while (ticks > 0)
                {
                    tick += ticks;
                    int layer = -1;
                    int layers = reader.ReadInt16();
                    while (layers > 0)
                    {
                        layer += layers;
                        while (LAYERS.Count <= layer)
                            LAYERS.Add(new Layer(LAYERS.Count + 1));

                        int inst = reader.ReadByte();
                        int pitch = reader.ReadByte();
                        reader.ReadBytes(1 * 2); // volume, panning
                        int finepitch = reader.ReadInt16();
                        LAYERS[layer].Add(tick, inst + 1, pitch);
                        layers = reader.ReadInt16();
                    }
                    ticks = reader.ReadInt16();
                }
                foreach (Layer layer in LAYERS)
                    layer.CalcAvgPitch();
                LAYERS.Sort(Comparer<Layer>.Create(
                        (k1, k2) => k2.GetAvgPitch().CompareTo(k1.GetAvgPitch())
                ));

                int i = 0;
                int bookIndex = -1;
                while (i < LAYERS.Count)
                {
                    List<Layer> TO_BOOK = new List<Layer>();
                    int sumLen = 0;
                    while (i < LAYERS.Count)
                    {
                        if (LAYERS[i].Notes.Count == 0)
                            continue;
                        int newLen = LAYERS[i].StrLen + LAYERS[i].Notes[LAYERS[i].Notes.Count - 1].Tick - LAYERS[i].Notes.Count;
                        if (sumLen + newLen > Page.MAX_PAGE_LEN * 99)
                        {
                            if (TO_BOOK.Count == 0)
                                throw new Exception("Слишком длинный слой #" + LAYERS[i].Num + "(много нот, а не тиков)");
                            break;
                        }
                        TO_BOOK.Add(LAYERS[i]);
                        sumLen += newLen;
                        i++;
                    }
                    bookIndex++;
                    books.Add(new List<string>());
                    int defaultInst = TO_BOOK[0].DefaultInst; // TODO choose most frequently used
                    string settings = setting_nbs + "default=" + defaultInst + ",";
                    int maxTick = 0;
                    foreach (Layer layer in LAYERS)
                    {
                        int lastTick = layer.Notes[layer.Notes.Count - 1].Tick;
                        if (lastTick > maxTick)
                            maxTick = lastTick;
                    }
                    tick = 0;
                    List<int> indexes = new List<int>();
                    for (int j = 0; j < TO_BOOK.Count; j++)
                        indexes.Add(0);

                    Page page = new Page(settings);
                    while (TO_BOOK.Count > 0)
                    {
                        int first = maxTick;
                        for (int j = 0; j < TO_BOOK.Count; j++)
                        {
                            int firstIndex = TO_BOOK[j].Notes[indexes[j]].Tick;
                            if (firstIndex < first)
                                first = firstIndex;
                        }
                        int silenceTicks = first - tick;
                        if (silenceTicks > 1)
                        {
                            silenceTicks -= 1;
                            string dots = new string('.', silenceTicks) + ",";
                            List<string> res1 = page.Add(dots);
                            foreach (string p in res1)
                            {
                                books[bookIndex].Add(p);
                            }
                        }
                        tick = first;
                        bool isFirst = true;
                        string tickText = "";
                        for (int j = TO_BOOK.Count - 1; j >= 0; j--)
                        {
                            NoteInfo note = TO_BOOK[j].Notes[indexes[j]];
                            if (note.Tick == tick)
                            {
                                if (isFirst)
                                    isFirst = false;
                                else
                                    tickText += "&";

                                string printed = note.Note;
                                if (TO_BOOK[j].DefaultInst != defaultInst && TO_BOOK[j].DefaultInst == note.Inst)
                                    printed = note.Inst + "-" + printed;
                                tickText += printed;
                                indexes[j]++;
                                if (indexes[j] >= TO_BOOK[j].Notes.Count)
                                {
                                    TO_BOOK.RemoveAt(j);
                                    indexes.RemoveAt(j);
                                }
                            }
                        }
                        if (TO_BOOK.Count > 0)
                            tickText += ",";
                        List<string> res2 = page.Add(tickText);
                        foreach (string p in res2)
                        {
                            books[bookIndex].Add(p);
                        }
                    }
                    books[bookIndex].Add(page.GetText());
                }
            }
            return books;
        }

        public static void ToFile(string origNbsFile, List<List<string>> books)
        {
            string pathOut = origNbsFile.Substring(0, origNbsFile.Length - ".nbs".Length) + ".txt";
            FileStream outStream = new FileStream(pathOut, FileMode.Create);
            using (StreamWriter output = new StreamWriter(outStream))
            {
                for (int i = 0; i < books.Count; i++)
                {
                    List<string> pages = books[i];
                    for (int j = 0; j < pages.Count; j++)
                    {
                        output.Write(PAGE_SEP);
                        output.Write(pages[j]);
                    }
                    output.Write(BOOK_SEP);
                }
            }
        }

        /** 0 => A0, 87 => C8 */
        public static string PitchToNote(int NbsPitch)
        {
            int octave = (NbsPitch + 9) / 12;
            int semitone = NbsPitch % 12;
            string note;
            switch (semitone)
            {
                case 0: note = "A"; break;
                case 1: note = "A#"; break;
                case 2: note = "B"; break;
                case 3: note = "C"; break;
                case 4: note = "C#"; break;
                case 5: note = "D"; break;
                case 6: note = "D#"; break;
                case 7: note = "E"; break;
                case 8: note = "F"; break;
                case 9: note = "F#"; break;
                case 10: note = "G"; break;
                case 11: note = "G#"; break;
                default: note = "@"; break;
            }
            return note + octave;
        }

        public static int GetWidth(char c)
        {
            if (c == '.' || c == ',')
                return 1;
            return 5; // 1234567890 ABCDEFG#-&
        }

        private class NoteInfo
        {
            public readonly int Tick;
            public readonly int Inst;
            public readonly string Note;

            public NoteInfo(int tick, int inst, string note)
            {
                Tick = tick;
                Inst = inst;
                Note = note;
            }
        }

        private class Layer
        {
            public readonly int Num;
            public readonly List<NoteInfo> Notes = new List<NoteInfo>();
            public int StrLen { get; private set; } = 0;
            public int DefaultInst { get; private set; } = -1;
            private double PitchSum = 0;
            private double AvgPitch = 0;

            public Layer(int num)
            {
                Num = num;
            }

            public void Add(int tick, int inst, int NbsPitch)
            {
                string note = PitchToNote(NbsPitch);
                if (inst != DefaultInst)
                {
                    if (DefaultInst < 0)
                        DefaultInst = inst;
                    else
                        note = inst + "-" + note;
                }
                Notes.Add(new NoteInfo(tick, inst, note));
                StrLen += note.Length;
                PitchSum += NbsPitch;
            }

            public void CalcAvgPitch()
            {
                if (Notes.Count > 0)
                    AvgPitch = PitchSum / Notes.Count;
            }

            public double GetAvgPitch()
            {
                return AvgPitch;
            }
        }

        private class Page
        {
            public static readonly int MAX_PAGE_LEN = 320;
            private static readonly int MAX_RAW_LEN = 57 * 2 - 1;
            private static readonly int MAX_RAW_COUNT = 14;
            private static readonly int INIT_RAW_COUNT = MAX_RAW_COUNT / 2 + 1;

            private string Text = "";
            private int Raw = INIT_RAW_COUNT;
            private int Len = 0;

            public Page(string initText)
            {
                if (initText.Length > MAX_PAGE_LEN)
                    throw new Exception("Too long Page init string, len=" + initText.Length);
                Text = initText;
            }

            public List<string> Add(string s)
            {
                int l1 = Text.Length;
                int count = s.Length;
                int i = 0;
                List<string> res = new List<string>();
                while (i < count)
                {
                    int width = GetWidth(s[i]);
                    if (Len + width > MAX_RAW_LEN)
                    {
                        Len = 0;
                        Raw++;
                    }
                    if (Raw > MAX_RAW_COUNT)
                    {
                        l1 -= Text.Length;
                        Raw = 1;
                        res.Add(Text);
                        Text = "";
                    }
                    Text += s[i];
                    Len += width + 1;
                    i++;
                    if (l1 + i + 1 > MAX_PAGE_LEN)
                    {
                        l1 -= MAX_PAGE_LEN;
                        Len = 0;
                        Raw = 1;
                        res.Add(Text);
                        Text = "";
                    }
                }
                return res;
            }

            public string GetText()
            {
                return Text;
            }
        }
    }
}
