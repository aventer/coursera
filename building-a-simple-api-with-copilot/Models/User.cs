using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Generators;

public class User
{
    // BCrypt work factor used when hashing passwords
    private const int PasswordHashCost = 12;

    // Unique Identifier for the User that is randomized and in combination with the Email forms the Id
    private Int16 IdSeed { get; } = GenerateIdSeed();

    // Unique Identifier for the User that is a combination of the IdSeed and the Email as a hash; (IdSeed + Email) = Hash
    public byte[] Id { get; private set; } = [];

    // Id as unpadded base64url (RFC 4648 §5): short and safe to use in URLs; case-sensitive, so compare it ordinally
    public string EncodedId => Base64Url.EncodeToString(Id);

    public string Name { get; set; } = null!;

    // Normalized so the same address always produces the same Id; the Id is re-derived whenever the Email changes
    // Uses the compiler generated backing `field` so the accessors can never reference Email itself (no recursion)
    public string Email
    {
        get;
        set
        {
            field = value?.Trim().ToLowerInvariant() ?? string.Empty;
            Id = GenerateId(IdSeed, field);
        }
    } = null!;

    // Hashed Password
    public string Password { get; set; } = null!;

    // Parameterless constructor for model binding / deserialization
    public User() { }

    // Creates a new User; the IdSeed and Id are generated automatically and the plain text password is hashed
    public User(string name, string email, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        Name = name.Trim();
        Email = email;
        Password = HashPassword(password);
    }

    // Replaces the stored hash with a hash of the new plain text password
    public void ChangePassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        Password = HashPassword(password);
    }

    // Checks a plain text password against the stored BCrypt hash
    public bool VerifyPassword(string password)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(Password))
        {
            return false;
        }

        return OpenBsdBCrypt.CheckPassword(Password, password.ToCharArray());
    }

    // Random value across the full Int16 range
    private static Int16 GenerateIdSeed()
    {
        return (Int16)RandomNumberGenerator.GetInt32(Int16.MinValue, Int16.MaxValue + 1);
    }

    // (IdSeed + Email) = SHA-256 Hash
    private static byte[] GenerateId(Int16 idSeed, string email)
    {
        byte[] seedBytes = BitConverter.GetBytes(idSeed);
        byte[] emailBytes = Encoding.UTF8.GetBytes(email);
        return SHA256.HashData([.. seedBytes, .. emailBytes]);
    }

    // BCrypt hash with a random 16 byte salt, stored in the standard $2y$ format
    private static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        return OpenBsdBCrypt.Generate(password.ToCharArray(), salt, PasswordHashCost);
    }
}
