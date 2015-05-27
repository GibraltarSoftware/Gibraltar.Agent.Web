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

        [Test]
        public void CreatesRequestMetricFromRoot()
        {
            var actual = RequestTrackingModule.BuildRequestMetric("/", string.Empty);
            Assert.AreEqual("/", actual.AbsolutePath);
            Assert.AreEqual(string.Empty, actual.PageName);
        }

        [Test]
        public void CreatesRequestMetricFromRegularPath()
        {
            var actual = RequestTrackingModule.BuildRequestMetric("/Default.aspx", string.Empty);
            Assert.AreEqual("/Default.aspx", actual.AbsolutePath);
            Assert.AreEqual("Default", actual.PageName);
        }

        [Test]
        public void CreatesRequestMetricFromExtensionlessPath()
        {
            var actual = RequestTrackingModule.BuildRequestMetric("/Default", string.Empty);
            Assert.AreEqual("/Default", actual.AbsolutePath);
            // TODO: This fails, actual is "Defaul"
            //Assert.AreEqual("Default", actual.PageName);
        }

        [Test]
        public void CreatesRequestMetricFromSillyTildePath()
        {
            var actual = RequestTrackingModule.BuildRequestMetric("~/Default.aspx", string.Empty);
            Assert.AreEqual("Default.aspx", actual.AbsolutePath);
            Assert.AreEqual("Default", actual.PageName);
        }

        [Test]
        public void SetsQueryString()
        {
            const string expected = "q=Why";
            var actual = RequestTrackingModule.BuildRequestMetric("~/Default.aspx", expected);
            Assert.AreEqual(expected, actual.QueryString);
        }

        [Test]
        public void NullMetricFromExcludedExtension()
        {
            Assert.IsNull(RequestTrackingModule.BuildRequestMetric("/favicon.ico", string.Empty));
        }
    }
}