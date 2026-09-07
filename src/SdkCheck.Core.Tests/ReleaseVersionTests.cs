public class ReleaseVersionTests
{
    [Test]
    [Arguments("8.0.424", "8.0.424")]
    [Arguments("11.0.100-rc.1.26431.118", "11.0.100")]
    [Arguments("8.0.0-rc.2", "8.0.0")]
    public async Task Numeric(string input, string expected) =>
        await Assert.That(ReleaseVersion.Numeric(input)).IsEqualTo(expected);

    [Test]
    [Arguments("8.0.424", false)]
    [Arguments("8.0.0-rc.2", true)]
    public async Task IsPrerelease(string input, bool expected) =>
        await Assert.That(ReleaseVersion.IsPrerelease(input)).IsEqualTo(expected);

    /// <summary>
    /// Version.Parse("8.0.0-rc.2") throws FormatException("The input string '0-rc' was not in a
    /// correct format"). Every channel that has had a preview carries release-versions in that
    /// shape, so a raw feed value reaching Version.Parse crashes the check outright.
    /// </summary>
    [Test]
    public async Task PrereleaseDoesNotThrow()
    {
        await Assert.That(() => Version.Parse("8.0.0-rc.2")).Throws<FormatException>();
        await Assert.That(ReleaseVersion.ParseNumeric("8.0.0-rc.2")).IsEqualTo(new(8, 0, 0));
    }

    [Test]
    [Arguments("")]
    [Arguments("   ")]
    [Arguments("not-a-version")]
    [Arguments("latest")]
    public async Task UnparseableIsNull(string input) =>
        await Assert.That(ReleaseVersion.ParseNumeric(input)).IsNull();

    [Test]
    public async Task NullIsNull() =>
        await Assert.That(ReleaseVersion.ParseNumeric(null)).IsNull();

    [Test]
    [Arguments("8.0.424", "8.0")]
    [Arguments("10.0.100", "10.0")]
    [Arguments("11.0.100-rc.1.26431.118", "11.0")]
    [Arguments("6.0.36", "6.0")]
    public async Task Channel(string input, string expected) =>
        await Assert.That(ReleaseVersion.Channel(input)).IsEqualTo(expected);

    [Test]
    public async Task ChannelOfGarbageIsNull() =>
        await Assert.That(ReleaseVersion.Channel("banana")).IsNull();
}
