# TranslateResx
open-source project that provides a simple and efficient way to translate .resx resource files into different languages
# TranslateResx

TranslateResx is an open-source project that provides a solution for translating .NET resource files (`.resx`) into different languages. It uses the Azure.AI.Translation.Text library to perform the translations.

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

Usage

Set your Azure Cognitive Services subscription key and endpoint in the Program.cs file.
<your_subscription_key_here> should be replaced with your Azure Text Translation service subscription key.
<path_to_your_source_file_here> should be replaced with the full path to your source .resx file. For example, it might be something like "C:\\Users\\YourName\\source\\repos\\YourProject\\Resources\\Strings.resx".
<path_to_your_target_file_here> should be replaced with the full path where you want the translated .resx file to be saved. For example, it might be something like "C:\\Users\\YourName\\source\\repos\\YourProject\\Resources\\Strings.de.resx".
<target_language_here> should be replaced with the language code for the language you want to translate to. For example, for German, it would be "de".

Run the program with .NET CLI

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
