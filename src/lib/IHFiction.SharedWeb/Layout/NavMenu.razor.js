export function registerOutsideClickListener(navbarRoot, dotNetRef) {
  const handlePointerDown = (event) => {
    if (!navbarRoot) {
      return;
    }

    const target = event.target;
    if (!(target instanceof Node)) {
      return;
    }

    if (navbarRoot.contains(target)) {
      return;
    }

    dotNetRef.invokeMethodAsync('HandleOutsideClick');
  };

  document.addEventListener('pointerdown', handlePointerDown, true);

  return {
    dispose: () => {
      document.removeEventListener('pointerdown', handlePointerDown, true);
    }
  };
}
