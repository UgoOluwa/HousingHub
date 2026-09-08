using HousingHub.Core.Security;

namespace HousingHub.Test.Security;

/// <summary>
/// Which address rate limiting counts against.
/// </summary>
/// <remarks>
/// A pen test reported the login and password-reset limits as bypassable by rotating
/// X-Forwarded-For, because the resolver read the leftmost entry — the part the
/// client writes. These tests pin the direction it reads from.
/// </remarks>
public class ClientAddressResolverTests
{
    private const string GatewayAddress = "203.0.113.9";
    private const string TransportPeer = "10.0.0.1";

    /// <summary>
    /// The bypass, as reported. The client prepends whatever it likes; the entry the
    /// trusted proxy appended is last, and that is the one that must win.
    /// </summary>
    [Theory]
    [InlineData("1.2.3.4")]
    [InlineData("9.9.9.9")]
    [InlineData("1.2.3.4, 5.6.7.8")]
    [InlineData("attacker-supplied-junk")]
    public void ASpoofedPrefix_DoesNotChangeTheAddress(string spoofed)
    {
        var resolved = ClientAddressResolver.Resolve($"{spoofed}, {GatewayAddress}", TransportPeer);

        Assert.Equal(GatewayAddress, resolved);
    }

    [Fact]
    public void ASingleEntry_IsUsed()
    {
        Assert.Equal(GatewayAddress, ClientAddressResolver.Resolve(GatewayAddress, TransportPeer));
    }

    [Fact]
    public void WithNoHeader_FallsBackToTheTransportPeer()
    {
        Assert.Equal(TransportPeer, ClientAddressResolver.Resolve(null, TransportPeer));
        Assert.Equal(TransportPeer, ClientAddressResolver.Resolve("", TransportPeer));
        Assert.Equal(TransportPeer, ClientAddressResolver.Resolve("   ", TransportPeer));
    }

    /// <summary>
    /// A junk tail must not send the search back to the client-controlled head —
    /// otherwise appending one unparseable entry restores the bypass.
    /// </summary>
    [Fact]
    public void AJunkTail_WalksInwardRatherThanFallingBackToTheHead()
    {
        var resolved = ClientAddressResolver.Resolve($"1.2.3.4, {GatewayAddress}, notanip", TransportPeer);

        Assert.Equal(GatewayAddress, resolved);
    }

    [Fact]
    public void WhenNothingInTheHeaderParses_FallsBackToTheTransportPeer()
    {
        Assert.Equal(TransportPeer, ClientAddressResolver.Resolve("junk, morejunk", TransportPeer));
    }

    [Fact]
    public void APortIsStripped()
    {
        Assert.Equal(GatewayAddress, ClientAddressResolver.Resolve($"1.2.3.4, {GatewayAddress}:41234", TransportPeer));
    }

    [Fact]
    public void ABracketedIpv6AddressIsRead()
    {
        Assert.Equal("2001:db8::1", ClientAddressResolver.Resolve("1.2.3.4, [2001:db8::1]:443", TransportPeer));
    }

    [Fact]
    public void ABareIpv6AddressIsRead()
    {
        Assert.Equal("2001:db8::1", ClientAddressResolver.Resolve("1.2.3.4, 2001:db8::1", TransportPeer));
    }

    [Fact]
    public void EmptyEntriesAndWhitespaceAreIgnored()
    {
        Assert.Equal(GatewayAddress, ClientAddressResolver.Resolve($" , 1.2.3.4 ,  , {GatewayAddress} , ", TransportPeer));
    }

    /// <summary>
    /// Two callers behind the same trusted proxy share a bucket, which is the
    /// accepted cost of reading the trustworthy end.
    /// </summary>
    [Fact]
    public void TwoCallersBehindOneProxy_ResolveToTheSameAddress()
    {
        var first = ClientAddressResolver.Resolve($"1.1.1.1, {GatewayAddress}", TransportPeer);
        var second = ClientAddressResolver.Resolve($"2.2.2.2, {GatewayAddress}", TransportPeer);

        Assert.Equal(first, second);
    }
}
