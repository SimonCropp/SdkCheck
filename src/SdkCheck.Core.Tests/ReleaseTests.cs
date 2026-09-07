public class ReleaseTests
{
    /// <summary>
    /// One release commonly ships several feature bands - the newest in "sdk", all of them in "sdks"
    /// - and an installed SDK matches whichever member carries it.
    /// </summary>
    [Test]
    public async Task ShipsSdkReadsBothMembers()
    {
        var release = new Release
        {
            Sdk = new()
            {
                Version = "8.0.204"
            },
            Sdks =
            [
                new()
                {
                    Version = "8.0.204"
                },
                new()
                {
                    Version = "8.0.106"
                }
            ]
        };

        await Assert.That(release.ShipsSdk("8.0.204")).IsTrue();
        await Assert.That(release.ShipsSdk("8.0.106")).IsTrue();
        await Assert.That(release.ShipsSdk("8.0.100")).IsFalse();
    }

    /// <summary>
    /// A member the feed left without a version matches nothing, rather than matching a component
    /// whose version is equally blank. Callers drop blank versions long before this, so the only
    /// thing this decides is what a malformed feed does.
    /// </summary>
    [Test]
    public async Task BlankVersionsShipNothing()
    {
        var release = new Release
        {
            Sdk = new()
            {
                Version = ""
            },
            Sdks =
            [
                new()
                {
                    Version = " "
                }
            ]
        };

        await Assert.That(release.ShipsSdk("")).IsFalse();
        await Assert.That(release.ShipsSdk(" ")).IsFalse();
        await Assert.That(release.SdkVersions()).IsEmpty();
    }
}
