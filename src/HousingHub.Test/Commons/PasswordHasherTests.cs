using System.Security.Cryptography;
using HousingHub.Service.Commons.Authentication;

namespace HousingHub.Test.Commons;

/// <summary>
/// Covers the stored-hash format change and the crash it replaced.
/// </summary>
/// <remarks>
/// Two things here are worth more than the rest. First, that hashes written before
/// the iteration count was recorded still verify — getting that wrong locks out
/// every existing user, and it is not the kind of mistake that shows up until
/// production. Second, that a malformed stored value returns false instead of
/// throwing: <c>Verify</c> previously did <c>passwordHash.Split('-')[1]</c> with no
/// length check, so an empty hash raised IndexOutOfRangeException, which escaped as
/// a 500 and distinguished a Google-only account from a wrong password.
/// </remarks>
public class PasswordHasherTests
{
    private readonly PasswordHasher _sut = new();

    private const string Password = "Correct-Horse-1";

    /// <summary>Reproduces the pre-change format: hash-salt, 500,000 iterations, no prefix.</summary>
    private static string LegacyHash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 500_000, HashAlgorithmName.SHA512, 32);

        return $"{Convert.ToHexString(hash)}-{Convert.ToHexString(salt)}";
    }

    // ── Round trip ───────────────────────────────────────────────

    [Fact]
    public void Hash_ThenVerify_Succeeds()
    {
        Assert.True(_sut.Verify(Password, _sut.Hash(Password)));
    }

    [Fact]
    public void Verify_RejectsTheWrongPassword()
    {
        Assert.False(_sut.Verify("not-the-password", _sut.Hash(Password)));
    }

    [Fact]
    public void Hash_IsSaltedSoTheSamePasswordHashesDifferently()
    {
        Assert.NotEqual(_sut.Hash(Password), _sut.Hash(Password));
    }

    [Fact]
    public void Hash_RecordsItsIterationCount()
    {
        // The whole point of the format change: the cost parameter travels with the
        // hash, so raising the constant later doesn't invalidate what's already stored.
        var parts = _sut.Hash(Password).Split('-');

        Assert.Equal(3, parts.Length);
        Assert.True(int.TryParse(parts[0], out var iterations));
        Assert.Equal(210_000, iterations);
    }

    // ── Backwards compatibility ──────────────────────────────────

    [Fact]
    public void Verify_AcceptsHashesWrittenBeforeTheIterationCountWasRecorded()
    {
        // If this fails, every account that existed before the change can no longer
        // sign in.
        Assert.True(_sut.Verify(Password, LegacyHash(Password)));
    }

    [Fact]
    public void Verify_StillRejectsAWrongPasswordAgainstALegacyHash()
    {
        Assert.False(_sut.Verify("not-the-password", LegacyHash(Password)));
    }

    [Fact]
    public void NeedsRehash_IsTrueForLegacy_AndFalseForCurrent()
    {
        Assert.True(_sut.NeedsRehash(LegacyHash(Password)));
        Assert.False(_sut.NeedsRehash(_sut.Hash(Password)));
    }

    [Fact]
    public void RehashingALegacyHash_ProducesOneThatStillVerifies()
    {
        var legacy = LegacyHash(Password);
        Assert.True(_sut.Verify(Password, legacy));

        var upgraded = _sut.Hash(Password);

        Assert.True(_sut.Verify(Password, upgraded));
        Assert.False(_sut.NeedsRehash(upgraded));
    }

    // ── Malformed input must not throw ───────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-separator-at-all-just-words")]
    [InlineData("nothexadecimal-alsonothex")]
    [InlineData("AABB")]                          // one part
    [InlineData("0-AABB-CCDD")]                   // zero iterations
    [InlineData("-1-AABB-CCDD")]                  // negative, and splits into four
    [InlineData("notanumber-AABB-CCDD")]          // unparseable iteration count
    [InlineData("AABB-CCDD")]                     // right shape, wrong lengths
    [InlineData("210000-AABB-CCDD")]              // right shape, wrong lengths
    public void Verify_ReturnsFalseRatherThanThrowing(string storedHash)
    {
        var exception = Record.Exception(() => _sut.Verify(Password, storedHash));

        Assert.Null(exception);
        Assert.False(_sut.Verify(Password, storedHash));
    }

    [Fact]
    public void Verify_ReturnsFalseForAnEmptyStoredHash()
    {
        // The specific case that produced the 500: a Google-registered account whose
        // PasswordHash was never set.
        Assert.False(_sut.Verify(Password, string.Empty));
    }

    [Fact]
    public void NeedsRehash_ReturnsFalseForUnparseableInput()
    {
        // Nothing to upgrade, and Verify rejects it anyway — reporting true would send
        // the caller down a re-hash path for a credential that didn't authenticate.
        Assert.False(_sut.NeedsRehash("garbage"));
        Assert.False(_sut.NeedsRehash(string.Empty));
    }
}
