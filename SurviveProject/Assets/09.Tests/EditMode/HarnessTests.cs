using NUnit.Framework;
using Survive.Domain;

public class HarnessTests
{
    [Test]
    public void 테스트_어셈블리가_도메인_어셈블리를_참조한다()
    {
        Assert.AreEqual("Survive.Domain", BuildMarker.AssemblyName);
    }
}
