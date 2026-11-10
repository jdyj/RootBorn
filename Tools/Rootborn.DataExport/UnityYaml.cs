using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Rootborn.DataExport
{
    public abstract class YNode { }

    public sealed class YScalar : YNode
    {
        public YScalar(string value, bool plain) { Value = value ?? ""; Plain = plain; }
        public string Value { get; }
        /// <summary>따옴표 없이 쓰인 값. 숫자로 볼 수 있는 것은 이것뿐이다.</summary>
        public bool Plain { get; }
    }

    public sealed class YSeq : YNode
    {
        public List<YNode> Items { get; } = new List<YNode>();
    }

    /// <summary>순서를 지키는 맵. 같은 키가 두 번 나와도 예외를 내지 않고 첫 값을 돌려준다.</summary>
    public sealed class YMap : YNode
    {
        public List<KeyValuePair<string, YNode>> Entries { get; } = new List<KeyValuePair<string, YNode>>();
        public string[] Keys => Entries.Select(e => e.Key).ToArray();

        public YNode Get(string key)
        {
            foreach (var e in Entries) if (e.Key == key) return e.Value;
            return null;
        }

        public string Text(string key) => (Get(key) as YScalar)?.Value;
    }

    public sealed class UnityDoc
    {
        public int ClassId;
        public string FileId;
        public string RootName;
        public YMap Body;
    }

    /// <summary>Unity가 쓰는 YAML만 읽는다. 문서 머리(--- !u!114 &amp;id)는 직접 자르고 본문은 YamlDotNet 이벤트로 읽는다.</summary>
    public static class UnityYaml
    {
        private static readonly Regex DocHeader = new Regex(@"^--- !u!(\d+) &(-?\d+)", RegexOptions.Compiled);

        public static List<UnityDoc> ParseDocs(string text)
        {
            var docs = new List<UnityDoc>();
            UnityDoc current = null;
            StringBuilder body = null;
            foreach (string line in text.Split('\n'))
            {
                Match m = line.StartsWith("--- ", StringComparison.Ordinal) ? DocHeader.Match(line) : Match.Empty;
                if (m.Success)
                {
                    Finish(docs, current, body);
                    current = new UnityDoc { ClassId = int.Parse(m.Groups[1].Value), FileId = m.Groups[2].Value };
                    body = new StringBuilder();
                }
                else if (body != null)
                {
                    body.Append(line).Append('\n');
                }
            }
            Finish(docs, current, body);
            return docs;
        }

        private static void Finish(List<UnityDoc> docs, UnityDoc doc, StringBuilder body)
        {
            if (doc == null) return;
            if (ParseNode(body.ToString()) is YMap root && root.Entries.Count > 0)
            {
                doc.RootName = root.Entries[0].Key;
                doc.Body = root.Entries[0].Value as YMap ?? new YMap();
            }
            else
            {
                doc.Body = new YMap();
            }
            docs.Add(doc);
        }

        public static YNode ParseNode(string yaml)
        {
            var parser = new Parser(new StringReader(yaml));
            parser.Consume<StreamStart>();
            if (!parser.TryConsume<DocumentStart>(out _)) return null;
            return Read(parser);
        }

        private static YNode Read(IParser parser)
        {
            if (parser.TryConsume<Scalar>(out var scalar))
                return new YScalar(scalar.Value, scalar.Style == ScalarStyle.Plain);
            if (parser.TryConsume<MappingStart>(out _))
            {
                var map = new YMap();
                while (!parser.TryConsume<MappingEnd>(out _))
                {
                    string key = parser.Consume<Scalar>().Value;
                    map.Entries.Add(new KeyValuePair<string, YNode>(key, Read(parser)));
                }
                return map;
            }
            if (parser.TryConsume<SequenceStart>(out _))
            {
                var seq = new YSeq();
                while (!parser.TryConsume<SequenceEnd>(out _)) seq.Items.Add(Read(parser));
                return seq;
            }
            throw new InvalidDataException("지원하지 않는 YAML 요소: " + parser.Current);
        }
    }
}
