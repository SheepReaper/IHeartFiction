### In case I forget

State: builds and runs

Wip:

1. Updated openapi return types but did not finish wiring up .WithLinks() on all endpoints(at a minimum each item should have a self link)
2. Story Editor is in broken state while I finish adding Multi-Chapter and Book support
3. Most of the UI is expecting shapes that the api endpoints (see 1.) isn't providing yet (though it just may work since the only real difference is adding the links property which nothing uses yet)
4. Paginated lists on the UI aren't tested.
5. Readme doesn't have any build instructions yet. (should just be dotnet build and dotnet run or aspire run for local dev)
6. A bunch of cleanup items: decide wether to go ham with the try-catch blocks or rely on the global exception handler stack, delete dead code, etc.