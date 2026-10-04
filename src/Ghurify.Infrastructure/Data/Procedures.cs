namespace Ghurify.Infrastructure.Data;

/// <summary>
/// Every stored procedure name used by the repositories, in one place.
/// Repositories reference these constants so a renamed procedure is a compile error
/// instead of a runtime one. Names follow the intent prefixes from the database rules:
/// Query (many rows), Get (one row), Add, Set, Del.
/// </summary>
public static class Procedures
{
    public static class Main
    {
        // Procedures arrive with their feature; none exist yet in the bootstrap schema.
        public const string Schema = "Main";
    }

    public static class Pay
    {
        public const string Schema = "Pay";
    }

    public static class Social
    {
        public const string Schema = "Social";
    }

    public static class Safety
    {
        public const string Schema = "Safety";
    }
}
