## IHeartFiction

### IDE requirements (If using Visual Studio)
- Version: 2019 Preview (16.2.0 preview 3.0) OR 2019 with experimental frameworks enabled.
- Extension: WebCompiler
- Extension: Blazor
- Extension: Microsoft Library Manager

### SDK Requirements
- Microsoft .NET Core SDK 3.0 preview 6

### Build steps
- Make sure client side libraries are restored in FictionScraper.Client (should happen automatically, but w/e)
- You can debug FictionScraper.Server in VS, but I usually just use `dotnet watch run` from a shell for automatic rebuilds.

### Troubleshooting
If you get an error from web compiler stating that the path to 7z.dll is inaccessible, you may need to run visual studio as admin once to clear this up. (Bug in web compiler setup: https://github.com/madskristensen/WebCompiler/issues/390)
