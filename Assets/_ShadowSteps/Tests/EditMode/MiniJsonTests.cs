using System.Collections.Generic;
using NUnit.Framework;
using ShadowSteps.Data;

namespace ShadowSteps.Tests.EditMode
{
    public class MiniJsonTests
    {
        [Test]
        public void ParsesNestedStructures()
        {
            var v = MiniJson.Parse("{\"a\": [1, 2.5, \"x\", true, null, {\"b\": [[0, 1]]}], \"c\": -3}");
            var o = (JsonObject)v;
            var a = (List<object>)o["a"];
            Assert.AreEqual(1L, a[0]);
            Assert.AreEqual(2.5, a[1]);
            Assert.AreEqual("x", a[2]);
            Assert.AreEqual(true, a[3]);
            Assert.IsNull(a[4]);
            var inner = (List<object>)((List<object>)((JsonObject)a[5])["b"])[0];
            Assert.AreEqual(0L, inner[0]);
            Assert.AreEqual(-3L, o["c"]);
        }

        [Test]
        public void ParsesStringEscapes()
        {
            Assert.AreEqual("a\"b\\c\ndé", MiniJson.Parse("\"a\\\"b\\\\c\\nd\\u00e9\""));
        }

        [TestCase("{")]
        [TestCase("[1,]")]
        [TestCase("{\"a\":1,}")]
        [TestCase("tru")]
        [TestCase("{\"a\" 1}")]
        [TestCase("[1] x")]
        public void RejectsMalformedInput(string json)
        {
            Assert.Throws<JsonParseException>(() => MiniJson.Parse(json));
        }

        [Test]
        public void SerializeThenParse_RoundTripsAndKeepsKeyOrder()
        {
            var o = new JsonObject().Set("z", 1L).Set("a", new List<object> { 1L, 2L }).Set("m", new JsonObject().Set("k", "v\n"));
            string json = MiniJson.Serialize(o);
            StringAssert.StartsWith("{\n  \"z\": 1,\n  \"a\": [1, 2]", json);
            var back = (JsonObject)MiniJson.Parse(json);
            CollectionAssert.AreEqual(new[] { "z", "a", "m" }, back.Keys);
            Assert.AreEqual("v\n", ((JsonObject)back["m"])["k"]);
        }

        [Test]
        public void IntAccessor_RejectsFractions()
        {
            Assert.Throws<LevelDataException>(() => JsonAccess.AsInt(1.5, "x"));
            Assert.AreEqual(2, JsonAccess.AsInt(2.0, "x"));
        }
    }
}
