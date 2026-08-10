namespace HousingHub.Service.Commons.Authentication;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);

    /// <summary>
    /// True when the stored hash was produced with parameters we have since changed,
    /// so the caller should re-hash and persist. Only meaningful immediately after a
    /// successful <see cref="Verify"/>, since re-hashing requires the plaintext.
    /// </summary>
    bool NeedsRehash(string passwordHash);
}
