## IHeartFiction

### IDE requirements
- Extension: WebCompiler
- Extension: Blazor
- Extension: Microsoft Library Manager

### Build steps
- Make sure client side libraries are restored in FictionScraper.Client (should happen automatically, but w/e)
- You can debug FictionScraper.Server in VS, but I usually just use `dotnet watch run` from a shell for automatic rebuilds.