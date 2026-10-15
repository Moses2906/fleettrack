// Remembers the dark/light choice in this browser. Kept in its own file because the
// Content Security Policy blocks inline scripts. Storage can be unavailable (private
// browsing, blocked site data), so every call fails quietly back to the default.
window.fleetTrackTheme = {
    get: function () {
        try { return window.localStorage.getItem("fleettrack-theme"); } catch { return null; }
    },
    set: function (value) {
        try { window.localStorage.setItem("fleettrack-theme", value); } catch { /* not saved; fine */ }
    }
};
