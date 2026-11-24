using NUnit.Framework;
using VF.Utils;

namespace VF.Tests {
    public class ReflectionHelperTest {
        [Test]
        public void RequiredBindingsResolve() {
            var missing = ReflectionHelper.GetMissingRequiredBindings();
            Assert.That(missing, Is.Empty, "Missing required reflection bindings:\n" + string.Join("\n", missing));
        }
    }
}
