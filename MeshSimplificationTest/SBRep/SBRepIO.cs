using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace MeshSimplificationTest.SBRep
{
    class StringSteamReader
    {
        private StreamReader sr;

        public StringSteamReader(StreamReader sr)
        {
            this.sr = sr;
            this.Separator = ' ';
        }

        private StringBuilder sb = new StringBuilder();
        public string ReadWord()
        {
            eol = false;
            sb.Clear();
            char c;
            while (!sr.EndOfStream)
            {
                c = (char)sr.Read();
                if (c == Separator) break;
                if (IsNewLine(c))
                {
                    eol = true;
                    char nextch = (char)sr.Peek();
                    while (IsNewLine(nextch))
                    {
                        sr.Read(); // consume all newlines
                        nextch = (char)sr.Peek();
                    }
                    break;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        private bool IsNewLine(char c)
        {
            return c == '\r' || c == '\n';
        }

        public int ReadInt()
        {
            var word = ReadWord();
            if (string.IsNullOrEmpty(word))
                word = ReadWord();
            return int.Parse(word, System.Globalization.CultureInfo.InvariantCulture);
        }

        public double ReadDouble()
        {
            var word = ReadWord();
            if (string.IsNullOrEmpty(word))
                word = ReadWord();
            return double.Parse(word, System.Globalization.CultureInfo.InvariantCulture);
        }

        public bool EOF
        {
            get { return sr.EndOfStream; }
        }

        public char Separator { get; set; }

        bool eol;
        public bool EOL
        {
            get { return eol || sr.EndOfStream; }
        }

        public T ReadObject<T>() where T : IParsable, new()
        {
            var obj = new T();
            obj.Parse(this);
            return obj;
        }

        public int[] ReadIntArray()
        {
            int size = ReadInt();
            var a = new int[size];
            for (int i = 0; i < size; i++)
            {
                a[i] = ReadInt();
            }
            return a;
        }

        public double[] ReadDoubleArray()
        {
            int size = ReadInt();
            var a = new double[size];
            for (int i = 0; i < size; i++)
            {
                a[i] = ReadDouble();
            }
            return a;
        }

        public T[] ReadObjectArray<T>() where T : IParsable, new()
        {
            int size = ReadInt();
            var a = new T[size];
            for (int i = 0; i < size; i++)
            {
                a[i] = ReadObject<T>();
            }
            return a;
        }

        internal void NextLine()
        {
            eol = false;
        }
    }

    interface IParsable
    {
        void Parse(StringSteamReader r);
    }

    public static class SBRepIO
    {
        public static async void Write(SBRepObject obj, string path)
        {
            using (StreamWriter writer = new StreamWriter(path, false))
            {
                Write(obj, writer).Wait();
            }
        }

        public static async Task Write(SBRepObject obj, StreamWriter writer)
        {
            await writer.WriteLineAsync(obj.Vertices.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (var item in obj.Vertices)
            {
                await writer.WriteLineAsync(item.ID.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await writer.WriteLineAsync(item.Coordinate.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await writer.WriteLineAsync(item.Coordinate.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await writer.WriteLineAsync(item.Coordinate.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            await writer.WriteLineAsync(obj.Edges.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (var item in obj.Edges)
            {
                await writer.WriteLineAsync(item.ID.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await writer.WriteLineAsync(item.Vertices.a.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await writer.WriteLineAsync(item.Vertices.b.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            await writer.WriteLineAsync(obj.Verges.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (var item in obj.Verges)
            {
                await writer.WriteLineAsync(item.ID.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await writer.WriteLineAsync(item.Edges.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                foreach (var edge in item.Edges)
                {
                    await writer.WriteLineAsync(edge.ToString(System.Globalization.CultureInfo.InvariantCulture));

                }
            }

            await writer.WriteLineAsync(obj.Loops.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (var item in obj.Loops)
            {
                await writer.WriteLineAsync(item.ID.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await writer.WriteLineAsync(item.Verges.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                foreach (var verge in item.Verges)
                {
                    await writer.WriteLineAsync(verge.ToString(System.Globalization.CultureInfo.InvariantCulture));

                }
            }

            await writer.WriteLineAsync(obj.Faces.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (var item in obj.Faces)
            {
                await writer.WriteLineAsync(item.ID.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await writer.WriteLineAsync(item.GroupID.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await writer.WriteLineAsync(item.OutsideLoop.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await writer.WriteLineAsync(item.InsideLoops.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                foreach (var loop in item.InsideLoops)
                {
                    await writer.WriteLineAsync(loop.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }

                await writer.WriteLineAsync(item.Plane.A.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await writer.WriteLineAsync(item.Plane.B.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await writer.WriteLineAsync(item.Plane.C.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await writer.WriteLineAsync(item.Plane.D.ToString(System.Globalization.CultureInfo.InvariantCulture));

                await writer.WriteLineAsync(item.Plane.Normal.x.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await writer.WriteLineAsync(item.Plane.Normal.y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                await writer.WriteLineAsync(item.Plane.Normal.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        public static SBRepObject Read(string path)
        {
            SBRepObject obj = null;
            using (StreamReader reader = new StreamReader(path))
            {
                obj = Read(reader);
            }
            return obj;
        }

        public static SBRepObject Read(StreamReader streamReader)
        {
            var obj = new SBRepObject();
            var reader = new StringSteamReader(streamReader);

            int count = -1;

            count = reader.ReadInt();
            for (int i = 0; i < count; ++i)
            {
                int id = reader.ReadInt();
                var x = reader.ReadDouble();
                var y = reader.ReadDouble();
                var z = reader.ReadDouble();
                obj.Vertices.Add(new SBRep_Vtx()
                {
                    ID = id,
                    Coordinate = new g3.Vector3d(x, y, z)
                });
            }

            count = reader.ReadInt();
            for (int i = 0; i < count; ++i)
            {
                int id = reader.ReadInt();
                var a = reader.ReadInt();
                var b = reader.ReadInt();
                obj.Edges.Add(new SBRep_Edge()
                {
                    ID = id,
                    Vertices = new g3.Index2i(a, b)
                });
            }

            count = reader.ReadInt();
            for (int i = 0; i < count; ++i)
            {
                int id = reader.ReadInt();
                var edgesIds = reader.ReadIntArray();

                obj.Verges.Add(new SBRep_Verge()
                {
                    ID = id,
                    Edges = edgesIds

                });
            }

            count = reader.ReadInt();
            for (int i = 0; i < count; ++i)
            {
                int id = reader.ReadInt();
                var verges = reader.ReadIntArray();

                obj.Loops.Add(new SBRep_Loop()
                {
                    ID = id,
                    Verges = verges
                });
            }

            count = reader.ReadInt();
            for (int i = 0; i < count; ++i)
            {
                int id = reader.ReadInt();
                int groupId = reader.ReadInt();
                int outsideLoop = reader.ReadInt();
                var insideLoops = reader.ReadIntArray();

                var a = reader.ReadDouble();
                var b = reader.ReadDouble();
                var c = reader.ReadDouble();
                var d = reader.ReadDouble();

                var x = reader.ReadDouble();
                var y = reader.ReadDouble();
                var z = reader.ReadDouble();

                obj.Faces.Add(new SBRep_Face()
                {
                    ID = id,
                    GroupID = groupId,
                    InsideLoops = insideLoops,
                    OutsideLoop = outsideLoop,
                    Plane = new Utils.PlaneFace()
                    {
                        A = a,
                        B = b,
                        C = c,
                        D = d,
                        Normal = new g3.Vector3d(x, y, z)
                    }
                });
            }

            obj.RedefineFeedbacks();

            return obj;
        }
    }
}
