using System.Linq;
using NUnit.Framework;

namespace Rootborn.Ui.Tests
{
    public sealed class NoEngineReferenceTests
    {
        [Test]
        public void RootbornUi_DoesNotReferenceMonoGame()
        {
            string[] references = typeof(UiTree).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.That(references.Where(name => name.Contains("MonoGame") || name.Contains("Xna") || name.Contains("FNA")), Is.Empty);
        }
    }
}
