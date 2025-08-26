window.theme = {
    // This function is called by the inline script in the <head> to prevent flickering.
    // It determines and applies the theme based on localStorage or the user's OS preference.
    applyInitialTheme: () => {
        let theme = localStorage.getItem('theme');
        if (theme === null) {
            // No theme set in local storage, check OS preference
            if (window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches) {
                theme = 'light';
            } else {
                theme = 'dark'; // Default to dark if no preference or preference is dark
            }
        }
        // This function is defined below and will set the attribute and local storage
        window.theme.setTheme(theme);
    },

    // This function is called by the Blazor ThemeService to change the theme after initial load.
    setTheme: (theme) => {
        localStorage.setItem('theme', theme);
        document.documentElement.setAttribute('data-theme', theme);
    },

    // This function is for the ThemeService to read the initial theme from the DOM
    getTheme: () => {
        return document.documentElement.getAttribute('data-theme');
    }
};

// Immediately apply the theme on script load.
window.theme.applyInitialTheme();