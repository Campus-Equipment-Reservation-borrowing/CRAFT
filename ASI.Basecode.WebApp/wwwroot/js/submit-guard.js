// Disables a form's submit buttons once it actually submits, so a double click can't post twice.
// Runs in the bubbling phase on document, after inline confirm() handlers and jQuery validation.
document.addEventListener('submit', function (e) {
    var form = e.target;
    if (e.defaultPrevented || form.hasAttribute('data-no-guard') || (form.method || '').toLowerCase() !== 'post') return;
    form.querySelectorAll('button[type=submit], input[type=submit], button:not([type])').forEach(function (btn) {
        btn.disabled = true;
        if (btn.tagName === 'BUTTON' && !btn.querySelector('i')) btn.textContent = 'Working…';
    });
});
// Restore buttons when the page comes back from the bfcache (browser Back).
window.addEventListener('pageshow', function (e) {
    if (e.persisted) document.querySelectorAll('form button[disabled], form input[type=submit][disabled]').forEach(function (b) { b.disabled = false; });
});
