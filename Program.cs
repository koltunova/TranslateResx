using Azure.AI.Translation.Text;
using Azure;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Xml.Linq;
using System.Collections.Generic;

using System.IO;
/// <summary>
/// Entry point and main workflow for the TranslateResx console application.
/// The program allows you to translate .resx resource files using the
/// Azure Translator service. An interactive console menu guides the user
/// through translating files or cleaning up obsolete resources. The code is
/// heavily commented to help you understand how each step works.
/// </summary>
class Program
{
    /// <summary>
    /// Application entry point. Configures dependency injection, reads
    /// application settings and then starts a simple console menu loop.
    /// The method returns a task because translation operations are
    /// asynchronous.
    /// </summary>
    static async Task<int> Main(string[] args)
    {
        // Determine which configuration file to use based on the
        // DOTNET_ENVIRONMENT environment variable. When nothing is
        // specified we fall back to "Production" settings.
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

        // Build the configuration object that will read settings from
        // appsettings.json and an optional environment specific file
        // like appsettings.Development.json.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Set up dependency injection. Here we only register the
        // localization services which provide access to .resx files.
        var services = new ServiceCollection();
        services.AddLogging(); // <-- Add this line to register logging
        services.AddLocalization();
        services.AddSingleton(serviceProvider =>
        {
            var factory = serviceProvider.GetRequiredService<IStringLocalizerFactory>();
            // Load "Strings" resources using the L namespace
            return factory.Create("Strings", typeof(L.Strings).Assembly.FullName);
        });

        using var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<IStringLocalizer>();

        // Build the list of cultures directly from the language dictionary.
        CultureInfo[] supportedCultures = LanguageData.Names
            .Keys
            .Select(code => new CultureInfo(code))
            .ToArray();

        // Save the resources path so it can be reused when the menu is shown.
        string resourcesPath = configuration["Files:ResourcesPath"] ?? string.Empty;

        // Start a simple menu loop so the user can choose what to do next.
        // This loop continues until the user selects the Quit option.
        while (true)
        {
            Console.WriteLine();
            // Display the configured languages table every time the menu appears
            // so the user can easily see available resources.
            PrintLanguagesTable(supportedCultures, resourcesPath);

            Console.WriteLine();
            Console.WriteLine("Select an option:");
            Console.WriteLine("1. Translate resource file");
            Console.WriteLine("2. Translate missing strings");
            Console.WriteLine("3. Cleanup resource files");
            Console.WriteLine("4. Quality check resource file");
            Console.WriteLine("5. Help");
            Console.WriteLine("6. Quit");
            Console.Write(" > ");

            var choice = Console.ReadLine()?.Trim().ToLowerInvariant();
            Console.WriteLine();

            // React to the user's choice. Each menu item invokes a helper
            // method to keep Main clean and easy to read.
            switch (choice)
            {
                case "1":
                case "translate":
                    await RunTranslateInteractive(configuration);
                    break;
                case "2":
                case "missing":
                    await RunTranslateMissingInteractive(configuration);
                    break;
                case "3":
                case "cleanup":
                    RunCleanupInteractive();
                    break;
                case "4":
                case "quality":
                    await RunQualityCheckInteractive(configuration);
                    break;
                case "5":
                case "h":
                case "help":
                    ShowHelp(configuration);
                    break;
                case "6":
                case "q":
                case "quit":
                    return 0;
                default:
                    Console.WriteLine("Unknown option. Please try again.");
                    break;
            }
        }
    }

    /// <summary>
    /// Asks the user for the details required to translate a resource file
    /// and then calls <see cref="TranslateResxFile"/> with those values.
    /// </summary>
    private static async Task RunTranslateInteractive(IConfiguration configuration)
    {
        string resourcesDir = configuration["Files:ResourcesPath"] ?? string.Empty;
        string defaultSourceLanguage = "en";
        string? subscriptionKey = configuration["AzureTranslation:SubscriptionKey"];

        // Show the user which resources directory will be used.
        Console.WriteLine($"Resources directory: {resourcesDir}");

        // The UI now builds file names based on the languages the user enters.
        Console.Write($"Source language code [{defaultSourceLanguage}]: ");
        string? sourceLanguage = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(sourceLanguage))
            sourceLanguage = defaultSourceLanguage;

        Console.Write("Target language codes (separate with spaces or commas): ");
        string? targetLanguagesInput = Console.ReadLine();
        var targetLanguages = (targetLanguagesInput ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(part => part.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(lang => lang.Trim())
            .Where(lang => !string.IsNullOrWhiteSpace(lang))
            .ToArray();

        if (string.IsNullOrWhiteSpace(resourcesDir) ||
            targetLanguages.Length == 0)
        {
            Console.WriteLine("Resource directory and languages are required.");
            return;
        }

        string sourceFilePath = GetExistingResourceFilePath(resourcesDir, sourceLanguage, true);
        Console.WriteLine($"Source file: {sourceFilePath}");

        foreach (var lang in targetLanguages)
        {
            string targetFilePath = GetExistingResourceFilePath(resourcesDir, lang, true);
            Console.WriteLine();
            Console.WriteLine($"Target file: {targetFilePath}");
            Console.WriteLine($"Language: {lang}");

            await TranslateResxFile(subscriptionKey, sourceFilePath, targetFilePath, lang);
        }
    }

    /// <summary>
    /// Translates only missing entries in all target language files based on a source language.
    /// </summary>
    private static async Task RunTranslateMissingInteractive(IConfiguration configuration)
    {
        string resourcesDir = configuration["Files:ResourcesPath"] ?? string.Empty;
        string defaultSourceLanguage = "en";
        string? subscriptionKey = configuration["AzureTranslation:SubscriptionKey"];

        Console.WriteLine($"Resources directory: {resourcesDir}");

        Console.Write($"Source language code [{defaultSourceLanguage}]: ");
        string? sourceLanguage = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(sourceLanguage))
            sourceLanguage = defaultSourceLanguage;

        Console.Write("Target language codes (separate with spaces or commas, leave blank for all): ");
        string? targetLanguagesInput = Console.ReadLine();

        List<string> targetLanguages = (targetLanguagesInput ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(p => p.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Select(lang => lang.Trim())
            .Where(lang => !string.IsNullOrWhiteSpace(lang))
            .ToList();

        if (!targetLanguages.Any())
        {
            if (Directory.Exists(resourcesDir))
            {
                var all = Directory.GetFiles(resourcesDir, "Strings.*.resx")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .Select(name => name == "Strings" ? "en" : name.Substring("Strings.".Length))
                    .Distinct()
                    .Where(code => !string.Equals(code, sourceLanguage, StringComparison.OrdinalIgnoreCase));
                targetLanguages.AddRange(all);
            }
        }

        if (string.IsNullOrWhiteSpace(resourcesDir) || !targetLanguages.Any())
        {
            Console.WriteLine("Resource directory and languages are required.");
            return;
        }

        string sourceFilePath = GetExistingResourceFilePath(resourcesDir, sourceLanguage, true);
        Console.WriteLine($"Source file: {sourceFilePath}");

        foreach (var lang in targetLanguages)
        {
            string targetFilePath = Path.Combine(resourcesDir, $"Strings.{lang}.resx");
            Console.WriteLine();
            Console.WriteLine($"Updating missing translations in: {targetFilePath}");

            await ResxTranslator.TranslateMissingAsync(subscriptionKey ?? string.Empty, sourceFilePath, targetFilePath, lang);
        }
    }

    /// <summary>
    /// Prompts the user for a source .resx file and a list of target files
    /// that should be compared against it. This method simply gathers input
    /// and passes it to <see cref="RunCleanup"/>.
    /// </summary>
    private static void RunCleanupInteractive()
    {
        Console.Write("Source file: ");
        string? sourceFile = Console.ReadLine();

        Console.Write("Target files (space separated): ");
        string? targets = Console.ReadLine();
        var targetFiles = (targets ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        RunCleanup(sourceFile, targetFiles);
    }

    /// <summary>
    /// Interactively runs quality check on a translated resource file.
    /// </summary>
    private static async Task RunQualityCheckInteractive(IConfiguration configuration)
    {
        string resourcesDir = configuration["Files:ResourcesPath"] ?? string.Empty;
        string defaultSourceLanguage = "en";
        string? subscriptionKey = configuration["AzureTranslation:SubscriptionKey"];

        Console.WriteLine($"Resources directory: {resourcesDir}");

        Console.Write($"Reference language code [{defaultSourceLanguage}]: ");
        string? sourceLanguage = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(sourceLanguage))
            sourceLanguage = defaultSourceLanguage;

        Console.Write("Target language code: ");
        string? targetLanguage = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(targetLanguage))
            return;

        string sourceFilePath = GetExistingResourceFilePath(resourcesDir, sourceLanguage, true);
        string targetFilePath = GetExistingResourceFilePath(resourcesDir, targetLanguage, true);

        Console.WriteLine($"Checking quality from {targetFilePath} against {sourceFilePath}");

        var results = await TranslationQualityChecker.CheckQualityAsync(subscriptionKey ?? string.Empty, sourceFilePath, targetFilePath, sourceLanguage, targetLanguage);

        foreach (var r in results.OrderBy(r => r.Rating))
        {
            Console.WriteLine($"{r.Key}: {r.Rating}");
        }
    }

    /// <summary>
    /// Placeholder for cleanup functionality. In a real application this
    /// would remove resources from the target files that do not exist in the
    /// source file.
    /// </summary>
    private static void RunCleanup(string? sourceFile, IEnumerable<string> targetFiles)
    {
        Console.WriteLine($"Source file: {sourceFile}");
        Console.WriteLine("Target files:");
        foreach (var f in targetFiles)
        {
            Console.WriteLine($" - {f}");
        }

        Console.WriteLine("Cleanup functionality is not implemented yet.");
    }

    private static void ShowHelp(IConfiguration configuration)
    {
        string resourcesDir = configuration["Files:ResourcesPath"] ?? "<not configured>";

        Console.WriteLine("Help");
        Console.WriteLine();
        Console.WriteLine("1. Translate resource file");
        Console.WriteLine("   Uses Azure Translator to create or update language specific .resx files.");
        Console.WriteLine($"   Resource files are read from: {resourcesDir}");
        Console.WriteLine("   Leaving the source language blank defaults to 'en'.");
        Console.WriteLine("   Enter one or more target language codes separated by spaces.");
        Console.WriteLine();
        Console.WriteLine("2. Translate missing strings");
        Console.WriteLine("   Adds translations only for entries missing in target language files.");
        Console.WriteLine();
        Console.WriteLine("3. Cleanup resource files");
        Console.WriteLine("   Removes entries from target files that don't exist in the source file (not implemented yet).");
        Console.WriteLine();
        Console.WriteLine("4. Quality check resource file");
        Console.WriteLine("   Compares translations with a back-translation to rate their quality.");
        Console.WriteLine("   Reference language defaults to 'en'.");
        Console.WriteLine();
        Console.WriteLine("5. Help displays this message.");
        Console.WriteLine();
        Console.WriteLine("6. Quit ends the program.");
    }

    private static void PrintLanguagesTable(IEnumerable<CultureInfo> cultures, string resourcesPath)
    {
        Console.WriteLine("Supported cultures:");
        Console.WriteLine($"{"Code",-10} {"Name",-30} {"File",-20} {"Items",10} {"Updated",-20}");
        Console.WriteLine(new string('-', 92));
        foreach (var culture in cultures)
        {
            string name = LanguageData.Names.TryGetValue(culture.Name, out var display) ? display : culture.DisplayName;
            string fileName = string.Empty;
            string items = string.Empty;
            string updated = string.Empty;
            if (!string.IsNullOrWhiteSpace(resourcesPath))
            {
                string path = GetExistingResourceFilePath(resourcesPath, culture.Name, true);
                if (File.Exists(path))
                {
                    var info = new FileInfo(path);
                    fileName = info.Name;
                    try
                    {
                        var doc = XDocument.Load(path);
                        items = doc.Descendants("data").Count().ToString();
                    }
                    catch
                    {
                        items = "?";
                    }
                    updated = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
                }
            }
            Console.WriteLine($"{culture.Name,-10} {name,-30} {fileName,-20} {items,10} {updated,-20}");
        }
    }

    private static string GetExistingResourceFilePath(string baseDir, string languageCode, bool fallBackToDefault = false)
    {
        string fullPath = Path.Combine(baseDir, $"Strings.{languageCode}.resx");
        if (File.Exists(fullPath))
            return fullPath;

        try
        {
            var culture = new CultureInfo(languageCode);
            if (!string.Equals(culture.Name, culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
            {
                string shortPath = Path.Combine(baseDir, $"Strings.{culture.TwoLetterISOLanguageName}.resx");
                if (File.Exists(shortPath))
                    return shortPath;
            }
        }
        catch (CultureNotFoundException)
        {
            // Ignore invalid culture codes and just return the original path
        }

        if (fallBackToDefault)
        {
            try
            {
                var culture = new CultureInfo(languageCode);
                if (culture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase))
                {
                    string defaultPath = Path.Combine(baseDir, "Strings.resx");
                    if (File.Exists(defaultPath))
                        return defaultPath;
                }
            }
            catch (CultureNotFoundException)
            {
                // Ignore invalid culture codes and proceed
            }
        }

        return fullPath;
    }


    /// <summary>
    /// Reads a source .resx file, sends each string to the Azure Translator
    /// service and saves the results into a new .resx file.
    /// </summary>
    private static async Task TranslateResxFile(string? subscriptionKey, string? sourceFilePath, string? targetFilePath, string? targetLanguage)
    {
        if (subscriptionKey is null || sourceFilePath is null || targetFilePath is null || targetLanguage is null)
            return;
        await ResxTranslator.TranslateFileAsync(subscriptionKey, sourceFilePath, targetFilePath, targetLanguage);
    }

    /// <summary>
    /// Displays a simple progress bar in the console.
    /// </summary>
    /// <param name="current">Number of items processed so far.</param>
    /// <param name="total">Total number of items to process.</param>
    public static void ShowProgress(int current, int total)
    {
        const int barWidth = 50;
        double progress = (double)current / total;
        int position = (int)(progress * barWidth);

        Console.Write("\r[");
        Console.Write(new string('#', position));
        Console.Write(new string('-', barWidth - position));
        Console.Write($"] {progress:P0}");

        if (current == total)
        {
            Console.WriteLine();
        }
    }
}
