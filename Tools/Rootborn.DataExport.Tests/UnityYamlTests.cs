using NUnit.Framework;

namespace Rootborn.DataExport.Tests
{
    public sealed class UnityYamlTests
    {
        [Test]
        public void ParseDocs_SplitsDocumentsAndReadsClassAndFileId()
        {
            string text = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"
                + "--- !u!114 &-116508140400273725\nMonoBehaviour:\n  m_Name: Sub\n"
                + "--- !u!114 &11400000\nMonoBehaviour:\n  m_Name: Main\n  _id: a.b\n";

            var docs = UnityYaml.ParseDocs(text);

            Assert.AreEqual(2, docs.Count);
            Assert.AreEqual(114, docs[0].ClassId);
            Assert.AreEqual("-116508140400273725", docs[0].FileId);
            Assert.AreEqual("MonoBehaviour", docs[1].RootName);
            Assert.AreEqual("a.b", ((YScalar)docs[1].Body.Get("_id")).Value);
        }

        [Test]
        public void ParseNode_KeepsOrderStyleAndUnicode()
        {
            string yaml = "b: 1\na: \"\\uC694\\uB9AC\\uC0AC \\uCCB4\\uD5D8\n  \\uAD00\\uB828\"\nc: {fileID: 11400000, guid: abc, type: 2}\nd: []\ne: \nf:\n- x: 1\n- x: 2\n";

            var map = (YMap)UnityYaml.ParseNode(yaml);

            Assert.AreEqual(new[] { "b", "a", "c", "d", "e", "f" }, map.Keys);
            Assert.IsTrue(((YScalar)map.Get("b")).Plain);
            var a = (YScalar)map.Get("a");
            Assert.IsFalse(a.Plain);
            Assert.AreEqual("요리사 체험 관련", a.Value);
            Assert.AreEqual("abc", ((YScalar)((YMap)map.Get("c")).Get("guid")).Value);
            Assert.AreEqual(0, ((YSeq)map.Get("d")).Items.Count);
            Assert.AreEqual("", ((YScalar)map.Get("e")).Value);
            Assert.AreEqual(2, ((YSeq)map.Get("f")).Items.Count);
        }
    }
}
