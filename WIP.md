## In case I forget

**State:** builds and runs

**Wip:**

1. Updated openapi return types, but did not finish wiring up WithLinks() on all endpoints(at a minimum, each item should have a self link)
1. Story Editor is in a broken state while I finish adding Multi-Chapter and Book support
1. Most of the UI is expecting shapes that the api endpoints (see 1.) aren't providing yet (though it just may work since the only real difference is adding the links property, which nothing uses yet)
1. Paginated lists on the UI aren't tested.
1. A bunch of cleanup items: decide whether to go ham with the try-catch blocks or rely on the global exception handler stack, delete dead code, etc.
