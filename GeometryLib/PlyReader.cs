using g3;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GeometryLib
{
    public class TextFileReader
    {
        protected StreamReader _reader = null;

        public long StreamPosition { get { return _reader.BaseStream.Position; } }

        public TextFileReader(StreamReader reader)
        {
            _reader = reader;
        }

        private string ReadWord()
        {
            string separators = " \r\t\n";
            string s = "";
            char c;
            while (_reader.Peek() >= 0)
            {
                c = (char)_reader.Read();
                if (separators.IndexOf(c) >= 0)
                {
                    if (s != "")
                        break;
                }
                else s += c;
            };
            return s;
        }

        public float ReadSingle()
        {
            string s = ReadWord();
            s = s.Replace(".", System.Globalization.NumberFormatInfo.CurrentInfo.NumberDecimalSeparator);
            s = s.Replace(",", System.Globalization.NumberFormatInfo.CurrentInfo.NumberDecimalSeparator);
            return float.Parse(s, System.Globalization.NumberStyles.Float);
        }

        public double ReadDouble()
        {
            string s = ReadWord();
            s = s.Replace(".", System.Globalization.NumberFormatInfo.CurrentInfo.NumberDecimalSeparator);
            s = s.Replace(",", System.Globalization.NumberFormatInfo.CurrentInfo.NumberDecimalSeparator);
            return double.Parse(s, System.Globalization.NumberStyles.Float);
        }
        public int ReadInt()
        {
            string s = ReadWord();
            s = s.Replace(".", System.Globalization.NumberFormatInfo.CurrentInfo.NumberDecimalSeparator);
            s = s.Replace(",", System.Globalization.NumberFormatInfo.CurrentInfo.NumberDecimalSeparator);
            return int.Parse(s, System.Globalization.NumberStyles.Integer);
        }

        public string ReadString()
        {
            return ReadWord();
        }
    }

    public class PlyReader
    {
        public enum PlyFileFormat
        {
            Unknown,
            ASCII,
            Binary_little_endian,
            Binary_big_endian
        }

        private class PlyHeaderParams
        {
            public PlyFileFormat Format;
            public int VertexCount;
            public int TriangleCount;
            //public IEnumerable<string> Comments;
        }


        /// <summary>
        /// Уникальный идентификатор STL-reader
        /// </summary>
        public string ID => "806BDDB5-D3AF-462D-8540-7F5F809E1C68";

        /// <summary>
        /// Имя ридера
        /// </summary>
        public string Name => "Ply Reader";

        /// <summary>
        /// Имя производителя
        /// </summary>
        public string VendorName => "Simmakers Ltd.";

        /// <summary>
        /// Фильтр расширения ридера
        /// </summary>
        public static string Filter
        {
            get
            {
                return "Ply file (*.ply)|*.ply";
            }
        }

        //public List<ObjectInfo> Read(
        //    string path,
        //    GeneralSettings settings,
        //    CancellationToken cancellationToken,
        //    IProgress<double> progress)
        //{
        //    throw new NotImplementedException();
        //}

        public DMesh3 Read(string path)
        {
            var file = new FileInfo(path);
            if(!file.Exists)
                throw new Exception($"File {file.Name} not exist.");

            DMesh3 mesh = null;
            using(var reader = new StreamReader(file.FullName))
            {
                mesh = Read(reader);
            }
            return mesh;
        }

        public DMesh3 Read(StreamReader reader)
        {

            var plyHeaderParams = ReadHeader(reader);

            var resultObject = ReadBody(reader, plyHeaderParams);

            if (resultObject == null)
                throw new Exception("Unknown format of .ply file");


            return resultObject;
        }

        private PlyFileFormat RecognizePlyFormat(string[] words)
        {
            var format = PlyFileFormat.Unknown;

            if (words != null && words.Length > 1)
            {
                var word = words[1].ToLower();
                switch (word)
                {
                    case "ascii":
                        return PlyFileFormat.ASCII;
                    case "binary_little_endian":
                        return PlyFileFormat.Binary_little_endian;
                    case "binary_big_endian":
                        return PlyFileFormat.Binary_little_endian;
                    default:
                        return PlyFileFormat.Unknown;
                }
            }

            return format;
        }

        private void ReadElementRegion(PlyHeaderParams header, string[] words)
        {
            if (words == null || words.Length <= 1)
                return;

            switch (words[1])
            {
                case "vertex":
                    header.VertexCount = int.Parse(words[2], CultureInfo.InvariantCulture);
                    break;
                case "face":
                    header.TriangleCount = int.Parse(words[2], CultureInfo.InvariantCulture);
                    break;
                default:
                    break;
            }
        }

        private PlyHeaderParams ReadHeader(StreamReader reader)
        {
            var header = new PlyHeaderParams();

            var plyDescriptor = reader.ReadLine();

            if (string.IsNullOrEmpty(plyDescriptor) || !plyDescriptor.Equals("ply"))
                throw new Exception("Ply file format has wrong format.");

            var end_header = false;
            while (!end_header)
            {
                var lineText = reader.ReadLine();
                if (string.IsNullOrEmpty(lineText))
                    throw new Exception();

                var words = lineText.Split(' ');

                if (words.Length < 1)
                    throw new Exception();

                var param = words[0].ToLower();

                switch (param)
                {
                    case "format":
                        var format = RecognizePlyFormat(words);
                        if (format == PlyFileFormat.Unknown)
                            throw new Exception("Unknown format of .ply file");
                        break;

                    case "comment":
                        break;

                    case "element":
                        ReadElementRegion(header, words);
                        break;

                    case "property":
                        break;

                    case "end_header":
                        end_header = true;
                        break;

                    default:
                        throw new Exception("Unknown format of .ply file");
                }
            }

            return header;
        }

        private DMesh3 ReadBody(StreamReader reader, PlyHeaderParams headerParams)
        {

            var tfr = new TextFileReader(reader);

            var vertices = new List<Vector3d>();
            var triangles = new List<Index3i>();

            for (int i = 0; i < headerParams.VertexCount; ++i)
            {
                var x = tfr.ReadDouble();
                var y = tfr.ReadDouble();
                var z = tfr.ReadDouble();
                vertices.Add(new Vector3d(x, y, z));
            }

            for (int i = 0; i < headerParams.TriangleCount; ++i)
            {
                var cnt = tfr.ReadInt();
                if (cnt != 3)
                    throw new Exception("Unknown format of .ply file");

                var a = tfr.ReadInt();
                var b = tfr.ReadInt();
                var c = tfr.ReadInt();
                var triangle = new Index3i(a, b, c);
                triangles.Add(triangle);
            }

            var mesh = DMesh3Builder.Build<Vector3d, Index3i, Vector3f>(
                vertices,
                triangles
                );


            return mesh;
        }
    }
}
