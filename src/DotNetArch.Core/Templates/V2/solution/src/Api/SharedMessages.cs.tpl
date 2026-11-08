namespace {{App}}.Api;

/// <summary>
/// Marker type of the shared, localized API messages (<c>Resources/SharedMessages.resx</c> and one file per culture, for example
/// <c>SharedMessages.fa.resx</c>). Read them through <c>IStringLocalizer&lt;SharedMessages&gt;</c>; the culture comes from the request
/// (<c>Accept-Language</c>, the <c>culture</c> query value or cookie) and is limited to <c>Localization:SupportedCultures</c>.
/// </summary>
public sealed class SharedMessages;
