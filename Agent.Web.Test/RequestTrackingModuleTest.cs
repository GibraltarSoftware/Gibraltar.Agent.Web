namespace Agent.Web.Test
{
    using Gibraltar.Agent.Web;
    using NUnit.Framework;

    [TestFixture]
    public class RequestTrackingModuleTest
    {
        [Test]
        public void PngExtensionIsExcluded()
        {
            Assert.True(RequestTrackingModule.IsExcludedExtension("png"));
        }

        [Test]
        public void UpperCaseExtensionIsExcluded()
        {
            Assert.True(RequestTrackingModule.IsExcludedExtension("PNG"));
        }

        [Test]
        public void AspxExtensionIsNotExcluded()
        {
            Assert.False(RequestTrackingModule.IsExcludedExtension("aspx"));
        }
    }
}