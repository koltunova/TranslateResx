using System.Collections.Generic;

/// <summary>
/// Provides mappings between language codes (such as "en-US") and human
/// readable display names. This is useful for presenting friendly language
/// names to the user while still working with the ISO codes internally.
/// </summary>
public static class LanguageData
{
    /// <summary>
    /// Dictionary of language codes to display names.  
    /// Extend this list to support additional UI cultures in Actualog.
    /// </summary>
    public static readonly Dictionary<string, string> Names = new()
    {
        ["ar"] = "Arabic",
        ["az"] = "Azerbaijani",
        ["be-BY"] = "Belarusian",
        ["bg-BG"] = "Bulgarian",
        ["bn-BD"] = "Bengali (Bangladesh)",
        ["cs-CZ"] = "Czech (Czech Republic)",
        ["da-DK"] = "Danish (Denmark)",
        ["de-DE"] = "German (Germany)",
        ["el-GR"] = "Greek (Greece)",
        ["en-US"] = "English (United States)",
        ["es-ES"] = "Spanish (Spain)",
        ["et-EE"] = "Estonian (Estonia)",
        ["fa-IR"] = "Persian",
        ["fil-PH"] = "Filipino (Philippines)",
        ["fi-FI"] = "Finnish (Finland)",
        ["fr-FR"] = "French (France)",
        ["he-IL"] = "Hebrew (Israel)",
        ["hi-IN"] = "Hindi (India)",
        ["hu-HU"] = "Hungarian (Hungary)",
        ["hy-AM"] = "Armenian",
        ["id-ID"] = "Indonesian (Indonesia)",
        ["it-IT"] = "Italian (Italy)",
        ["ja-JP"] = "Japanese (Japan)",
        ["ka-GE"] = "Georgian (Georgia)",
        ["kk-KZ"] = "Kazakh (Kazakhstan)",
        ["ko-KR"] = "Korean (South Korea)",
        ["ky-KG"] = "Kyrgyz (Kyrgyzstan)",
        ["lt-LT"] = "Lithuanian (Lithuania)",
        ["lv-LV"] = "Latvian (Latvia)",
        ["ms-MY"] = "Malay (Malaysia)",
        ["my-MM"] = "Burmese (Myanmar)",
        ["nb-NO"] = "Norwegian Bokmål (Norway)",
        ["nl-NL"] = "Dutch (Netherlands)",
        ["pl-PL"] = "Polish (Poland)",
        ["pt-PT"] = "Portuguese (Portugal)",
        ["ro-RO"] = "Romanian (Romania)",
        ["ru-RU"] = "Russian (Russia)",
        ["sk-SK"] = "Slovak (Slovakia)",
        ["sl-SI"] = "Slovenian (Slovenia)",
        ["sv-SE"] = "Swedish (Sweden)",
        ["sw-KE"] = "Swahili (Kenya)",
        ["syr-SY"] = "Syriac (Syria)",
        ["ta-IN"] = "Tamil (India)",
        ["te-IN"] = "Telugu (India)",
        ["th-TH"] = "Thai (Thailand)",
        ["tr-TR"] = "Turkish (Turkey)",
        ["tt-RU"] = "Tatar (Russia)",
        ["uk-UA"] = "Ukrainian (Ukraine)",
        ["ur-PK"] = "Urdu (Pakistan)",
        ["uz-UZ"] = "Uzbek (Uzbekistan)",
        ["vi-VN"] = "Vietnamese (Vietnam)",
        ["zh-CHS"] = "Chinese (Simplified, China)",
        ["zh-CN"] = "Chinese (Simplified, Mainland China)",
        ["zh-TW"] = "Chinese (Traditional, Taiwan)"
    };
}

