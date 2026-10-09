using Ghurify.Application.Site;
using Ghurify.Domain.Site;

namespace Ghurify.UnitTests.Site;

/// <summary>
/// The rules that decide what a super admin may do to the site's own look.
///
/// The contrast checks matter most: every other rule here catches a typo, but an unreadable
/// theme can lock everybody out of the page they would use to put it right.
/// </summary>
public sealed class SiteSettingsTests
{
    [Fact]
    public void Catalogue_HasNoDuplicates_AndEveryDefaultIsValid()
    {
        var keys = SiteSettingsCatalog.All.Select(definition => definition.Key).ToList();

        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());

        // A default that its own rules would refuse would make "reset" impossible.
        Assert.All(
            SiteSettingsCatalog.All,
            definition => Assert.Null(SiteSettingRules.CheckValue(definition, definition.Default)));

        // Every default fits the column it is stored in.
        Assert.All(
            SiteSettingsCatalog.All,
            definition => Assert.InRange(definition.Default.Length, 0, definition.MaxLength));

        // A required setting with an empty default would leave the site with nothing to fall
        // back to.
        Assert.All(
            SiteSettingsCatalog.All.Where(definition => definition.Required),
            definition => Assert.NotEqual(string.Empty, definition.Default));
    }

    [Fact]
    public void TheShippedTheme_PassesItsOwnContrastRules()
    {
        // The whole point of the defaults: the site as it ships has to be readable, or the first
        // save of any unrelated setting would be refused.
        Assert.Empty(SiteSettingRules.CheckContrast(SiteSettingsCatalog.Defaults()));
    }

    [Theory]
    [InlineData("#245c43", true)]
    [InlineData("#FFF", false)]
    [InlineData("245c43", false)]
    [InlineData("#245c4", false)]
    [InlineData("#245c4g", false)]
    [InlineData("rgb(36,92,67)", false)]
    [InlineData("", false)]
    public void Colour_AcceptsOnlySixDigitHex(string input, bool valid)
    {
        Assert.Equal(valid, BrandColour.TryParse(input, out _));
    }

    [Fact]
    public void Contrast_MatchesTheKnownWcagRatios()
    {
        Assert.True(BrandColour.TryParse("#000000", out var black));
        var white = BrandColour.White;

        // Black on white is the maximum the formula can produce.
        Assert.Equal(21, BrandColour.Contrast(black, white), precision: 1);
        // A colour against itself is the minimum.
        Assert.Equal(1, BrandColour.Contrast(white, white), precision: 2);
        // Order does not matter.
        Assert.Equal(BrandColour.Contrast(black, white), BrandColour.Contrast(white, black), precision: 6);
    }

    [Fact]
    public void Contrast_RefusesAPrimaryTooFaintForWhite()
    {
        var theme = SiteSettingsCatalog.Defaults();
        // A pale mint: a plausible brand colour, and unreadable as text on a white page.
        theme[SiteSettingKeys.ColourHill] = "#e8f5ee";

        var problems = SiteSettingRules.CheckContrast(theme);

        Assert.NotEmpty(problems);
        Assert.All(problems, problem => Assert.Equal(SiteSettingKeys.ColourHill, problem.Key));
        Assert.All(problems, problem => Assert.Equal("setting_low_contrast", problem.Code));
    }

    [Fact]
    public void Contrast_RefusesAFooterTooLightForWhiteText()
    {
        var theme = SiteSettingsCatalog.Defaults();
        theme[SiteSettingKeys.ColourNight] = "#9fd6b8";

        Assert.Contains(
            SiteSettingRules.CheckContrast(theme),
            problem => problem.Key == SiteSettingKeys.ColourNight);
    }

    [Fact]
    public void Contrast_JudgesAPairEvenWhenOnlyOneSideChanged()
    {
        // Headings are fine on white but not on a tinted section this dark: the failure is in
        // the pair, which is why the check is given the whole theme rather than one colour.
        var theme = SiteSettingsCatalog.Defaults();
        theme[SiteSettingKeys.ColourMist] = "#2d6b50";

        var problems = SiteSettingRules.CheckContrast(theme);

        Assert.Contains(problems, problem => problem.Key == SiteSettingKeys.ColourDeep);
    }

    [Fact]
    public void Contrast_AllowsTurmericAsAFillThoughItCouldNotCarryTextOnWhite()
    {
        // Documented behaviour: turmeric is never a word on white, so it is judged against the
        // dark text that sits on it instead.
        Assert.True(BrandColour.TryParse("#d99a12", out var turmeric));
        Assert.True(BrandColour.Contrast(turmeric, BrandColour.White) < SiteSettingRules.MinimumTextContrast);

        Assert.DoesNotContain(
            SiteSettingRules.CheckContrast(SiteSettingsCatalog.Defaults()),
            problem => problem.Key == SiteSettingKeys.ColourTurmeric);
    }

    [Theory]
    [InlineData("https://facebook.com/ghurify", null)]
    [InlineData("https://www.facebook.com/ghurify", null)]
    [InlineData("https://m.facebook.com/ghurify", null)]
    [InlineData("http://facebook.com/ghurify", "setting_not_url")]
    [InlineData("facebook.com/ghurify", "setting_not_url")]
    [InlineData("https://user:pass@facebook.com/x", "setting_not_url")]
    [InlineData("https://evil.example.com/ghurify", "setting_wrong_host")]
    // The two that a naive "contains" or "ends with" check would wave through.
    [InlineData("https://facebook.com.evil.example.com/x", "setting_wrong_host")]
    [InlineData("https://notfacebook.com/x", "setting_wrong_host")]
    public void SocialLink_HasToBeHttpsOnThatPlatform(string url, string? expected)
    {
        var definition = SiteSettingsCatalog.Find(SiteSettingKeys.SocialFacebook)!;

        Assert.Equal(expected, SiteSettingRules.CheckValue(definition, url)?.Code);
    }

    [Fact]
    public void Value_RefusesControlCharacters()
    {
        var definition = SiteSettingsCatalog.Find(SiteSettingKeys.Name)!;

        // A newline in the site name would travel into the page title and into every email.
        Assert.Equal("setting_invalid", SiteSettingRules.CheckValue(definition, "Ghu\nrify")?.Code);
    }

    [Fact]
    public void Value_RefusesSomethingLongerThanItsColumn()
    {
        var definition = SiteSettingsCatalog.Find(SiteSettingKeys.Name)!;

        Assert.Equal("setting_too_long", SiteSettingRules.CheckValue(definition, new string('a', 41))?.Code);
    }

    [Fact]
    public void Value_TreatsEmptyAsResetRatherThanAsAnError()
    {
        // Empty means "back to the default" for every kind, including the required ones.
        Assert.All(
            SiteSettingsCatalog.All,
            definition => Assert.Null(SiteSettingRules.CheckValue(definition, string.Empty)));
    }

    [Theory]
    [InlineData("Barlow", null)]
    [InlineData("Comic Sans MS", "setting_unknown_font")]
    [InlineData("Poppins", "setting_unknown_font")]
    public void BodyFont_HasToBeOneWeCanLoad(string family, string? expected)
    {
        // Poppins is a heading family, not a body one: the lists are separate because the
        // weights each is loaded with differ.
        var definition = SiteSettingsCatalog.Find(SiteSettingKeys.FontBody)!;

        Assert.Equal(expected, SiteSettingRules.CheckValue(definition, family)?.Code);
    }

    [Fact]
    public void EveryOfferedFont_IsAcceptedByItsOwnRule()
    {
        var body = SiteSettingsCatalog.Find(SiteSettingKeys.FontBody)!;
        var display = SiteSettingsCatalog.Find(SiteSettingKeys.FontDisplay)!;

        Assert.All(FontChoices.Body, choice => Assert.Null(SiteSettingRules.CheckValue(body, choice.Family)));
        Assert.All(FontChoices.Display, choice => Assert.Null(SiteSettingRules.CheckValue(display, choice.Family)));
    }

    [Fact]
    public void FontStack_AlwaysKeepsTheBanglaFaces()
    {
        // Whatever Latin family is chosen, Bangla has to render: it is a correctness rule, not
        // a style choice.
        Assert.All(
            FontChoices.Body.Concat(FontChoices.Display),
            choice =>
            {
                var stack = FontChoices.Stack(choice.Family);
                Assert.Contains("Hind Siliguri", stack, StringComparison.Ordinal);
                Assert.Contains("Noto Sans Bengali", stack, StringComparison.Ordinal);
            });
    }

    [Fact]
    public void FontStylesheet_IsOnGoogleFonts_AndCarriesTheBanglaFaces()
    {
        var url = FontChoices.StylesheetUrl(FontChoices.Body[1], FontChoices.Display[2]);

        Assert.StartsWith("https://fonts.googleapis.com/css2?", url, StringComparison.Ordinal);
        Assert.Contains("Hind+Siliguri", url, StringComparison.Ordinal);
        Assert.Contains("Noto+Sans+Bengali", url, StringComparison.Ordinal);
        Assert.Contains("Inter", url, StringComparison.Ordinal);
        Assert.Contains("Playfair+Display", url, StringComparison.Ordinal);
        // Ordered and de-duplicated, so the same choices always give the same cacheable URL.
        Assert.Equal(url, FontChoices.StylesheetUrl(FontChoices.Body[1], FontChoices.Display[2]));
    }

    [Fact]
    public void FontStylesheet_DoesNotRepeatAFamilyChosenForBoth()
    {
        // Hind Siliguri is offered for both, and is also a Bangla fallback.
        var url = FontChoices.StylesheetUrl(FontChoices.Body[4], FontChoices.Display[4]);
        var occurrences = url.Split("family=Hind+Siliguri").Length - 1;

        Assert.Equal(1, occurrences);
    }

    // --- Building the configuration ---

    [Fact]
    public void Effective_UsesTheDefaultsWhenNothingIsSaved()
    {
        var values = SiteConfigService.Effective(new Dictionary<string, string>());

        Assert.Equal("Ghurify", values[SiteSettingKeys.Name]);
        Assert.Equal("#245c43", values[SiteSettingKeys.ColourHill]);
        Assert.Equal(SiteSettingsCatalog.All.Count, values.Count);
    }

    [Fact]
    public void Effective_IgnoresAKeyThisBuildDoesNotKnow()
    {
        // A newer release's setting, read by an older API: it must fall back rather than guess.
        var values = SiteConfigService.Effective(new Dictionary<string, string>
        {
            [SiteSettingKeys.Name] = "Bhromon",
            ["site.future.thing"] = "whatever",
        });

        Assert.Equal("Bhromon", values[SiteSettingKeys.Name]);
        Assert.DoesNotContain("site.future.thing", values.Keys);
    }

    [Fact]
    public void Effective_TreatsAnEmptyStoredValueAsTheDefault()
    {
        var values = SiteConfigService.Effective(new Dictionary<string, string>
        {
            [SiteSettingKeys.Name] = string.Empty,
        });

        Assert.Equal("Ghurify", values[SiteSettingKeys.Name]);
    }

    [Fact]
    public void Config_LeavesOutASocialPlatformWithNoLink()
    {
        var config = SiteConfigService.Build(new StoredSiteSettings(
            new Dictionary<string, string> { [SiteSettingKeys.SocialFacebook] = "https://facebook.com/x" },
            []));

        var link = Assert.Single(config.Social);
        Assert.Equal("facebook", link.Platform);
        Assert.Equal("https://facebook.com/x", link.Url);
    }

    [Fact]
    public void Config_PointsAtTheAppsOwnFilesUntilAnImageIsUploaded()
    {
        var config = SiteConfigService.Build(new StoredSiteSettings(new Dictionary<string, string>(), []));

        Assert.All(config.Assets, asset => Assert.False(asset.IsCustom));
        Assert.Equal("/favicon.svg", config.Assets.Single(asset => asset.Kind == "favicon").Url);
    }

    [Fact]
    public void Config_GivesAnUploadedImageAVersionedUrl()
    {
        var uploaded = new DateTimeOffset(2026, 10, 9, 6, 0, 0, TimeSpan.Zero);

        var config = SiteConfigService.Build(new StoredSiteSettings(
            new Dictionary<string, string>(),
            [new SiteAssetStamp("logo", "image/png", 2048, uploaded)]));

        var logo = config.Assets.Single(asset => asset.Kind == "logo");
        Assert.True(logo.IsCustom);
        // The version is what lets the API serve it with a long cache lifetime and still have a
        // replacement appear at once.
        Assert.Equal(
            $"/api/v1/site/assets/logo?v={uploaded.ToUnixTimeSeconds()}",
            logo.Url);
    }

    [Fact]
    public void Config_Version_ChangesWithTheContentAndNotOtherwise()
    {
        var empty = SiteConfigService.Build(new StoredSiteSettings(new Dictionary<string, string>(), []));
        var again = SiteConfigService.Build(new StoredSiteSettings(new Dictionary<string, string>(), []));
        var renamed = SiteConfigService.Build(new StoredSiteSettings(
            new Dictionary<string, string> { [SiteSettingKeys.Name] = "Bhromon" },
            []));

        // Stable across calls, so a browser's cached copy survives a restart.
        Assert.Equal(empty.Version, again.Version);
        Assert.NotEqual(empty.Version, renamed.Version);
    }

    [Fact]
    public void Config_FallsBackWhenAChosenFontIsNoLongerOffered()
    {
        // A release that drops a family must not leave the site with no font at all.
        var config = SiteConfigService.Build(new StoredSiteSettings(
            new Dictionary<string, string> { [SiteSettingKeys.FontBody] = "Gone Sans" },
            []));

        Assert.Equal(FontChoices.Body[0].Family, config.Theme.BodyFont);
    }

    [Fact]
    public void Config_NamesEveryColourTokenTheStylesheetUses()
    {
        var config = SiteConfigService.Build(new StoredSiteSettings(new Dictionary<string, string>(), []));

        // These are the names the web app sets as --color-* variables, so a rename here would
        // silently stop recolouring the site.
        Assert.Equal(
            ["deep", "hill", "jamdani", "mist", "night", "ochre", "turmeric"],
            config.Theme.Colours.Keys.Order(StringComparer.Ordinal));
    }
}
