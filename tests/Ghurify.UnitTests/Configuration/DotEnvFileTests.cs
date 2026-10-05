using Ghurify.Infrastructure.Configuration;

namespace Ghurify.UnitTests.Configuration;

/// <summary>Reading the local .env file into configuration keys.</summary>
public sealed class DotEnvFileTests
{
    [Fact]
    public void Parse_TurnsDoubleUnderscoresIntoNestedKeys()
    {
        var settings = DotEnvFile.Parse(["Email__Password=abcdefghijklmnop"]);

        Assert.Equal("abcdefghijklmnop", settings["Email:Password"]);
    }

    [Fact]
    public void Parse_SkipsCommentsBlankLinesAndEmptyValues()
    {
        // An unfilled line from the template must not blank out a setting from elsewhere.
        var settings = DotEnvFile.Parse(["# a comment", "", "   ", "Email__UserName=", "Email__Host=smtp.gmail.com"]);

        Assert.Equal(["Email:Host"], settings.Keys);
    }

    [Fact]
    public void Parse_TakesQuotedValuesLiterally()
    {
        // Connection strings and passwords routinely contain = ; # and spaces.
        var settings = DotEnvFile.Parse(
        [
            "Database__ConnectionString=\"Server=x;Password=a#b=c;\"",
            "Email__Password='with spaces # not a comment'",
        ]);

        Assert.Equal("Server=x;Password=a#b=c;", settings["Database:ConnectionString"]);
        Assert.Equal("with spaces # not a comment", settings["Email:Password"]);
    }

    [Fact]
    public void Parse_EndsAnUnquotedValueAtAnInlineComment()
    {
        var settings = DotEnvFile.Parse(["Email__Port=587 # STARTTLS"]);

        Assert.Equal("587", settings["Email:Port"]);
    }

    [Fact]
    public void Parse_AcceptsExportPrefixesAndIgnoresMalformedLines()
    {
        var settings = DotEnvFile.Parse(["export Email__Host=smtp.gmail.com", "not a setting", "=no-key"]);

        Assert.Equal("smtp.gmail.com", Assert.Single(settings).Value);
    }

    [Fact]
    public void Load_FindsTheFileInAParentFolder()
    {
        var root = Directory.CreateTempSubdirectory("ghurify-dotenv-");
        try
        {
            File.WriteAllText(Path.Combine(root.FullName, DotEnvFile.FileName), "Email__Host=smtp.gmail.com");
            var nested = Directory.CreateDirectory(Path.Combine(root.FullName, "src", "Ghurify.Api"));

            var settings = DotEnvFile.Load(nested.FullName);

            Assert.Equal("smtp.gmail.com", settings["Email:Host"]);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }
}
