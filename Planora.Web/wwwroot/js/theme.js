// Theme persistence (light/dark). The actual switch is just one HTML attribute — every color
// in app.css is a CSS custom property, so flipping `data-theme` re-paints the whole app instantly
// with no Blazor re-render needed. See the inline script in index.html for the pre-Blazor-load
// application of the saved preference (avoids a flash of the wrong theme on refresh).
window.planoraGetTheme = function () {
    return localStorage.getItem('planora-theme') || 'light';
};

window.planoraSetTheme = function (theme) {
    localStorage.setItem('planora-theme', theme);
    document.documentElement.setAttribute('data-theme', theme);
};
