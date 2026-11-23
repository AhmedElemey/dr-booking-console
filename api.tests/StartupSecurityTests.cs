namespace Console.Tests;

public sealed class StartupSecurityTests(PostgresFixture db) : IClassFixture<PostgresFixture>
{
    [Theory]
    [InlineData("Jwt:Key", "development-only-signing-key-0123456789abcdef")]
    [InlineData("Jwt:Key", "CHANGE-ME-signing-key-0123456789abcdef")]
    [InlineData("SuperAdmin:Password", "ChangeMe-12345")]
    [InlineData("SuperAdmin:Password", "short")]
    public async Task Non_development_refuses_public_or_short_credentials(string key, string value)
    {
        await using var factory = new ConsoleApiFactory(db.ConnectionString, new() { [key] = value });
        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }
}
