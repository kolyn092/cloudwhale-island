using NUnit.Framework;
using UnityEngine;

namespace CloudWhale.Tests
{
    public sealed class ProjectSetupTests
    {
        [Test]
        public void ProductName_IsCloudWhaleIsland()
        {
            Assert.That(Application.productName, Is.EqualTo("CloudWhale Island"));
        }
    }
}
