using System.Collections.Generic;

/// <summary>
/// Provides mappings between language codes (such as "en-US") and human
/// readable display names. This is useful for presenting friendly language
/// names to the user while still working with the ISO codes internally.
/// </summary>
public static class LanguageData
{
    /// <summary>
    /// Dictionary of language codes to display names. Add or remove entries
    /// here if your application needs to support additional languages.
    /// </summary>
    public static readonly Dictionary<string, string> Names = new()
    {
        ["en-US"] = "English (United States)",
        ["ar"] = "Arabic",
        ["hy-AM"] = "Armenian",
        ["az"] = "Azerbaijani",
        ["be-BY"] = "Belarusian",
        ["bg-BG"] = "Bulgarian",
        ["zh-CHS"] = "Chinese (Simplified, China)",
        ["cs-CZ"] = "Czech (Czech Republic)",
        ["da-DK"] = "Danish (Denmark)",
        ["nl-NL"] = "Dutch (Netherlands)",
        ["et-EE"] = "Estonian (Estonia)",
        ["fi-FI"] = "Finnish (Finland)",
        ["fr-FR"] = "French (France)",
        ["ka-GE"] = "Georgian (Georgia)",
        ["de-DE"] = "German (Germany)",
        ["el-GR"] = "Greek (Greece)",
        ["id-ID"] = "Indonesian (Indonesia)",
        ["it-IT"] = "Italian (Italy)",
        ["ja-JP"] = "Japanese (Japan)",
        ["ko-KR"] = "Korean (South Korea)",
        ["ky-KG"] = "Kyrgyz (Kyrgyzstan)",
        ["lv-LV"] = "Latvian (Latvia)",
        ["lt-LT"] = "Lithuanian (Lithuania)",
        ["hi-IN"] = "Hindi (India)",
        ["hu-HU"] = "Hungarian (Hungary)",
        ["kk-KZ"] = "Kazakh (Kazakhstan)",
        ["nb-NO"] = "Norwegian Bokm\u00e5l (Norway)",
        ["pl-PL"] = "Polish (Poland)",
        ["pt-PT"] = "Portuguese (Portugal)",
        ["sk-SK"] = "Slovak (Slovakia)",
        ["sl-SI"] = "Slovenian (Slovenia)",
        ["es-ES"] = "Spanish (Spain)",
        ["sv-SE"] = "Swedish (Sweden)",
        ["syr-SY"] = "Syriac (Syria)",
        ["ro-RO"] = "Romanian (Romania)",
        ["ru-RU"] = "Russian (Russia)",
        ["tt-RU"] = "Tatar (Russia)",
        ["th-TH"] = "Thai (Thailand)",
        ["tr-TR"] = "Turkish (Turkey)",
        ["uk-UA"] = "Ukrainian (Ukraine)",
        ["vi-VN"] = "Vietnamese (Vietnam)"
    };
}
