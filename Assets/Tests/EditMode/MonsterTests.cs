using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class MonsterTests
{
    [Test]
    public void MonsterInfo_DefaultValues_AreCorrect()
    {
        var info = new MonsterInfo();
        
        Assert.AreEqual(3f, info.moveSpeed);
        Assert.AreEqual(10f, info.trackingRange);
        Assert.AreEqual(1.5f, info.attackRange);
        Assert.AreEqual(8f, info.wanderRadius);
    }
}
