# TranslateResx
TranslateResx is an open-source project that provides a simple and efficient way to translate `.resx` resource files into different languages. It uses the `Azure.AI.Translation.Text` library to perform the translations.


## Features

- Translates .NET resource files into different languages.
- Handles HTML tags and markup in the resource files.
- Uses Azure.AI.Translation.Text for translations.

## Getting Started

### Prerequisites

- .NET Core 3.1 or later
- An Azure account with an active subscription - you will need a Subscription key.
Subscription key is a unique identifier that is used to authenticate requests associated with your project for using Azure Cognitive Services, such as the Text Translation API. It's also known as an API key. Azure uses this key to debit your account according to the number of transactions made by your application.

Here are the steps to get a subscription key for Azure Cognitive Services:
Sign in to the Azure portal: If you don't have an Azure account, you'll need to create one.
Create a resource: In the Azure portal, click on "Create a resource". Then, search for the service you want to use (in this case, "Translator Text"). Click on the service and then click "Create".
Fill in the details: You'll need to fill in some details about your resource. You'll need to choose a name, subscription, resource group, pricing tier, and region. Once you've filled in all the details, click "Review + create" and then "Create".
Get the key: Once your resource is created, go to "Resource Management" > "Keys and Endpoint". There you will find your key under "KEY1" or "KEY2". You can use either of these keys as your subscription key.
Remember to keep your subscription key secure! Do not share your subscription key in your public GitHub repository. If your key is exposed, someone else could use it and incur charges on your account. Instead, you can instruct users to replace a placeholder text with their own subscription key in the code.

Configuration

Update settings in `appsettings.json`. Use `appsettings.Development.json` or `appsettings.Production.json` for environment specific overrides. Set the `DOTNET_ENVIRONMENT` environment variable accordingly when running the tool.

Only a few values are required:

```
{
  "AzureTranslation": {
    "SubscriptionKey": "your_subscription_key"
  },
  "Files": {
    // Folder that holds Strings.<culture>.resx files
    "ResourcesPath": "path_to_resources"
  }
}
```

`ResourcesPath` should point to the directory that contains all your `.resx` files. Language codes are mapped to display names in `LanguageData.cs` so that the UI can show user-friendly names while the configuration continues to use codes.

When you run the application with no command line arguments a simple menu is displayed. Use the **Help** option at any time to see what each menu item does and what the default values are. Choose **Translate resource file** and the tool will use the resources directory configured in `appsettings.json`. The selected path is printed so you know which folder is being used. You are prompted only for the source language and one or more target languages. Leaving the source language blank defaults to `en`. The tool automatically builds file names like `Strings.en.resx` or `Strings.fr.resx` based on your input. Both two letter codes and full culture names are supported when locating files. If a resource file for `en` or `en-US` is missing the program will use `Strings.resx`; for all other languages the file must exist or an error is shown.

Whenever the menu is displayed the program prints a table of all
languages defined in `LanguageData.cs`. The table shows each language code,
its friendly name and information about any existing resource file found in
`ResourcesPath`, including how many items each file contains and the last
modification date.

Run the program with .NET CLI

### Command line usage

```
TranslateResx translate [--all|--missing] [--source <resx>] [--target <resx>] [--language <lang>]
TranslateResx cleanup --source <resx> --target <file1> [<file2> ...]
```

- `--all` translates every item in the target file.
- `--missing` translates only the items missing from the target file.
- `--source` allows you to specify which `Strings.resx` to use as the source of truth.
- `--language` sets the language to translate into.
- `cleanup` compares target `.resx` files with the source one and removes items not found in the source.


### Example

Run the tool without arguments and select **Translate resource file** from the menu.
When prompted you can press Enter to use the resources directory from `appsettings.json` and leave the source language blank to use `en`. Then enter one or more target languages separated by spaces. For instance entering `de fr` will create `Strings.de.resx` and `Strings.fr.resx` next to your source file.


Contributing
Contributions are what make the open-source community such an amazing place to learn, inspire, and create. Any contributions you make are greatly appreciated.

Fork the project
Create your feature branch (git checkout -b feature/AmazingFeature)
Commit your changes (git commit -m 'Add some AmazingFeature')
Push to the branch (git push origin feature/AmazingFeature)
Open a pull request

License
Distributed under the MIT License. See LICENSE for more information.

Contact
Kate Koltunova kate@actualog.com

Project Link: https://github.com/koltunova/TranslateResx
