namespace Controller;

/// <summary>The only place that touches password hashing. Uses BCrypt (salted, slow by design).</summary>
public class PasswordService
{
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            // A malformed hash must never crash the login; it simply does not match.
            return false;
        }
    }
}
