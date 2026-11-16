// Show/hide toggle for password inputs: a [data-password-toggle] button inside
// .cp-password-field switches its sibling [data-password-input] between password and text.
document.addEventListener('click', function (e) {
    var btn = e.target.closest('[data-password-toggle]');
    if (!btn) return;
    var input = btn.closest('.cp-password-field').querySelector('[data-password-input]');
    if (!input) return;
    var show = input.type === 'password';
    input.type = show ? 'text' : 'password';
    btn.setAttribute('aria-pressed', String(show));
    var label = show ? 'Hide password' : 'Show password';
    btn.setAttribute('aria-label', label);
    btn.setAttribute('title', label);
});
// Mask the password again before submit so the browser doesn't offer to save it as plain text.
document.addEventListener('submit', function (e) {
    e.target.querySelectorAll('[data-password-input]').forEach(function (input) { input.type = 'password'; });
}, true);
