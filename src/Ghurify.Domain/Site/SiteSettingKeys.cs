namespace Ghurify.Domain.Site;

/// <summary>
/// Every setting the super admin can change, by key.
///
/// A key is a dotted path, stored in <c>[Site].[Setting].[Key]</c> and written into the audit
/// log, so it reads the same in the panel, the database and the trail. The group is the first
/// segment, which is how the panel lays the screens out.
///
/// Adding one: add the constant, describe it in <see cref="SiteSettingsCatalog"/>, give it a
/// Bangla and English label in the web app's i18n files, and — if the site should render it —
/// read it where the hard-coded value used to be. A key the database holds but the code does not
/// define is ignored on read and refused on write, so a rollback can only ever fall back to a
/// default, never apply something this build does not understand.
/// </summary>
public static class SiteSettingKeys
{
    // --- Who the site is ---

    /// <summary>The name in the header, the tab title, emails and the footer.</summary>
    public const string Name = "site.name";

    public const string NameBn = "site.name.bn";

    /// <summary>The line under the name: "Travel together, safely".</summary>
    public const string Tagline = "site.tagline";

    public const string TaglineBn = "site.tagline.bn";

    /// <summary>The meta description, and what a link preview shows.</summary>
    public const string Description = "site.description";

    public const string DescriptionBn = "site.description.bn";

    // --- How to reach the company ---

    public const string ContactEmail = "contact.email";

    public const string ContactPhone = "contact.phone";

    public const string ContactAddress = "contact.address";

    public const string ContactAddressBn = "contact.address.bn";

    // --- Where the company is online ---

    public const string SocialFacebook = "social.facebook";

    public const string SocialInstagram = "social.instagram";

    public const string SocialYouTube = "social.youtube";

    public const string SocialX = "social.x";

    public const string SocialLinkedIn = "social.linkedin";

    public const string SocialTikTok = "social.tiktok";

    // --- Colours. Every screen draws from these; nothing in the app holds a hex value. ---

    /// <summary>The primary green: buttons, links, active states.</summary>
    public const string ColourHill = "theme.colour.hill";

    /// <summary>The darker green: headings and strong text.</summary>
    public const string ColourDeep = "theme.colour.deep";

    /// <summary>The darkest surface: the footer and the safety band.</summary>
    public const string ColourNight = "theme.colour.night";

    /// <summary>The bright accent, for fills only. Never text on white.</summary>
    public const string ColourTurmeric = "theme.colour.turmeric";

    /// <summary>The accent at a weight that can carry text on white.</summary>
    public const string ColourOchre = "theme.colour.ochre";

    /// <summary>The warning and error colour.</summary>
    public const string ColourJamdani = "theme.colour.jamdani";

    /// <summary>The alternate section background, a very light tint.</summary>
    public const string ColourMist = "theme.colour.mist";

    // --- Type ---

    /// <summary>Body font, from the allowed families (see <see cref="FontChoices"/>).</summary>
    public const string FontBody = "theme.font.body";

    /// <summary>Heading font, from the allowed families.</summary>
    public const string FontDisplay = "theme.font.display";
}

/// <summary>The part of the panel a setting belongs to. Decides which screen shows it.</summary>
public enum SettingGroup
{
    /// <summary>Name, tagline, description: who the site says it is.</summary>
    Identity = 1,

    Contact = 2,
    Social = 3,

    /// <summary>Colours and type.</summary>
    Theme = 4,
}

/// <summary>How a setting's value is written down, which decides how it is checked and edited.</summary>
public enum SettingKind
{
    /// <summary>One line of text.</summary>
    Text = 1,

    /// <summary>A paragraph.</summary>
    LongText = 2,

    /// <summary>A colour as <c>#rrggbb</c>.</summary>
    Colour = 3,

    /// <summary>An https URL.</summary>
    Url = 4,

    EmailAddress = 5,

    /// <summary>A Bangladeshi number in E.164 (<c>+8801XXXXXXXXX</c>).</summary>
    Phone = 6,

    /// <summary>One of a fixed list of font families.</summary>
    Font = 7,
}
