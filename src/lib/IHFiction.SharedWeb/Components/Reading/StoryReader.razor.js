let activeCleanup;

export function observe(element, dotNetReference) {
    activeCleanup?.();

    let activeSeconds = 0;
    let interacted = false;
    let qualified = false;
    let disposed = false;

    const markInteraction = () => { interacted = true; };
    const options = { passive: true };
    window.addEventListener("scroll", markInteraction, options);
    window.addEventListener("wheel", markInteraction, options);
    window.addEventListener("touchmove", markInteraction, options);
    window.addEventListener("pointerdown", markInteraction, options);
    window.addEventListener("keydown", markInteraction);

    const cleanup = () => {
        if (disposed) return;
        disposed = true;
        window.clearInterval(timer);
        window.removeEventListener("scroll", markInteraction, options);
        window.removeEventListener("wheel", markInteraction, options);
        window.removeEventListener("touchmove", markInteraction, options);
        window.removeEventListener("pointerdown", markInteraction, options);
        window.removeEventListener("keydown", markInteraction);
        domObserver.disconnect();
        if (activeCleanup === cleanup) activeCleanup = undefined;
    };

    const domObserver = new MutationObserver(() => {
        if (!element.isConnected) cleanup();
    });
    domObserver.observe(document.body, { childList: true, subtree: true });

    const timer = window.setInterval(async () => {
        if (disposed || qualified || document.hidden || !document.hasFocus()) return;
        if (!element.isConnected) {
            cleanup();
            return;
        }
        activeSeconds++;
        if (activeSeconds >= 10 && interacted) {
            qualified = true;
            window.clearInterval(timer);
            try {
                await dotNetReference.invokeMethodAsync("OnQualifiedRead", activeSeconds);
            } catch {
                // Interactive Server circuit or .NET reference ended while the timer was pending.
            } finally {
                cleanup();
            }
        }
    }, 1000);

    activeCleanup = cleanup;
}
