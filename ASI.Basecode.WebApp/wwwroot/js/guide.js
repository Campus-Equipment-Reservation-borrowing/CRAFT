/*
 * CRAFT guided walkthrough.
 * Tours are defined per role in guide-tours.js (window.CRAFT_TOURS). A tour is a list of steps;
 * each step highlights one element (by CSS selector, usually [data-guide="..."]) with a popover.
 * Steps can live on different pages: progress is kept in sessionStorage and resumed on load.
 *
 * Step fields:
 *   target  CSS selector to highlight (omit for a centred message)
 *   title, text
 *   page    path prefix the step belongs on (case-insensitive); defaults to the current page
 *   url     where to go when the user is not on `page` (omit when the user must pick an item, e.g. an id)
 *   click   true = the user continues by clicking the highlighted element (e.g. a link to the next page)
 *   note    shown when the target is missing (e.g. an empty list)
 */
(function () {
    'use strict';

    var STORE = 'craftGuide';
    var body = document.body;
    var role = (body.getAttribute('data-guide-role') || '').toUpperCase();
    var tours = (window.CRAFT_TOURS || {})[role] || [];
    if (!tours.length) return;

    var state = null; // { tour, step }
    var els = {};
    var reduceMotion = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    function tourById(id) { for (var i = 0; i < tours.length; i++) if (tours[i].id === id) return tours[i]; return null; }
    function save() { try { sessionStorage.setItem(STORE, JSON.stringify(state)); } catch (e) { /* storage blocked: tour still works on this page */ } }
    function clearSaved() { try { sessionStorage.removeItem(STORE); } catch (e) { } }
    function load() { try { return JSON.parse(sessionStorage.getItem(STORE) || 'null'); } catch (e) { return null; } }

    function onPage(step) {
        if (!step.page) return true;
        return location.pathname.toLowerCase().indexOf(step.page.toLowerCase()) === 0;
    }

    function token() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    // Server-side "seen" flag so the overview does not replay on other devices.
    function markCompleted(completed) {
        var data = new URLSearchParams();
        data.append('completed', completed ? 'true' : 'false');
        data.append('__RequestVerificationToken', token());
        return fetch('/Profile/TourCompleted', { method: 'POST', body: data, credentials: 'same-origin' }).catch(function () { });
    }

    function build() {
        if (els.pop) return;
        els.blocker = document.createElement('div');
        els.blocker.className = 'cp-guide-blocker';
        els.spot = document.createElement('div');
        els.spot.className = 'cp-guide-spot';
        els.pop = document.createElement('div');
        els.pop.className = 'cp-guide-pop';
        els.pop.setAttribute('role', 'dialog');
        els.pop.setAttribute('aria-modal', 'false');
        els.pop.setAttribute('aria-live', 'polite');
        els.pop.tabIndex = -1;
        els.pop.innerHTML =
            '<div class="cp-guide-step"></div>' +
            '<h2 class="cp-guide-title" id="cpGuideTitle"></h2>' +
            '<p class="cp-guide-body"></p>' +
            '<p class="cp-guide-note" hidden></p>' +
            '<p class="cp-guide-hint" hidden></p>' +
            '<div class="cp-guide-actions">' +
            '<button type="button" class="cp-guide-skip">Skip tour</button>' +
            '<button type="button" class="cp-btn cp-btn-secondary cp-btn-sm cp-guide-back">Back</button>' +
            '<button type="button" class="cp-btn cp-btn-primary cp-btn-sm cp-guide-next">Next</button>' +
            '</div>';
        els.pop.setAttribute('aria-labelledby', 'cpGuideTitle');
        document.body.appendChild(els.blocker);
        document.body.appendChild(els.spot);
        document.body.appendChild(els.pop);
        els.pop.querySelector('.cp-guide-skip').addEventListener('click', function () { finish(true); });
        els.pop.querySelector('.cp-guide-back').addEventListener('click', back);
        els.pop.querySelector('.cp-guide-next').addEventListener('click', next);
        window.addEventListener('resize', place);
        window.addEventListener('scroll', place, true);
        document.addEventListener('keydown', onKey);
    }

    function teardown() {
        ['blocker', 'spot', 'pop'].forEach(function (k) { if (els[k]) els[k].remove(); });
        els = {};
        window.removeEventListener('resize', place);
        window.removeEventListener('scroll', place, true);
        document.removeEventListener('keydown', onKey);
        if (clickTarget) { clickTarget.removeEventListener('click', onTargetClick, true); clickTarget = null; }
    }

    function onKey(e) {
        if (!els.pop) return;
        var typing = /^(INPUT|TEXTAREA|SELECT)$/.test((e.target || {}).tagName || '');
        if (e.key === 'Escape') { e.preventDefault(); finish(true); }
        else if (!typing && e.key === 'ArrowRight') { e.preventDefault(); next(); }
        else if (!typing && e.key === 'ArrowLeft') { e.preventDefault(); back(); }
    }

    function visible(el) {
        if (!el) return false;
        var r = el.getBoundingClientRect();
        return r.width > 0 && r.height > 0 && getComputedStyle(el).visibility !== 'hidden';
    }

    // Sidebar items are hidden when the menu is collapsed (phones); open it so they can be shown.
    function reveal(el) {
        if (visible(el)) return el;
        var sidebar = document.getElementById('appSidebar');
        var toggle = document.getElementById('sidebarToggle');
        if (sidebar && toggle && sidebar.contains(el)) {
            toggle.click();
        }
        return visible(el) ? el : null;
    }

    var current = null; // resolved target element for the current step
    var clickTarget = null;

    function onTargetClick() {
        // Continue on the next page (the click usually navigates); also advance if it does not.
        state.step += 1;
        save();
        setTimeout(function () { if (els.pop) show(); }, 400);
    }

    function show() {
        var tour = tourById(state.tour);
        if (!tour || state.step < 0 || state.step >= tour.steps.length) { finish(false); return; }
        var step = tour.steps[state.step];

        if (!onPage(step)) {
            if (step.url) { save(); location.href = step.url; return; }
        }

        build();
        if (clickTarget) { clickTarget.removeEventListener('click', onTargetClick, true); clickTarget = null; }

        var el = step.target && onPage(step) ? document.querySelector(step.target) : null;
        current = el ? reveal(el) : null;

        els.pop.querySelector('.cp-guide-step').textContent = tour.title + ' · Step ' + (state.step + 1) + ' of ' + tour.steps.length;
        els.pop.querySelector('.cp-guide-title').textContent = step.title;
        els.pop.querySelector('.cp-guide-body').textContent = step.text;

        var note = els.pop.querySelector('.cp-guide-note');
        var missing = step.target && !current;
        note.hidden = !missing;
        if (missing) {
            note.textContent = !onPage(step)
                ? (step.note || 'Open the page this step describes to see it highlighted.')
                : (step.note || "This part isn't on the page right now; it appears once there is data to show.");
        }

        var clickMode = !!(step.click && current);
        var hint = els.pop.querySelector('.cp-guide-hint');
        hint.hidden = !clickMode;
        hint.textContent = clickMode ? 'Click the highlighted item to continue, or press Next.' : '';
        // In click mode the page stays clickable so the user can use the highlighted control.
        els.blocker.style.display = clickMode ? 'none' : 'block';
        if (clickMode) { clickTarget = current; clickTarget.addEventListener('click', onTargetClick, true); }

        els.pop.querySelector('.cp-guide-back').disabled = state.step === 0;
        var last = state.step === tour.steps.length - 1;
        els.pop.querySelector('.cp-guide-next').textContent = last ? 'Finish' : 'Next';

        if (current) current.scrollIntoView({ block: 'center', inline: 'nearest', behavior: reduceMotion ? 'auto' : 'smooth' });
        place();
        setTimeout(place, reduceMotion ? 0 : 320); // re-measure after smooth scrolling settles
        els.pop.focus({ preventScroll: true });
        save();
    }

    function place() {
        if (!els.pop) return;
        var vw = window.innerWidth, vh = window.innerHeight, gap = 12, pad = 6;
        var pw = els.pop.offsetWidth, ph = els.pop.offsetHeight;

        if (!current || !visible(current)) {
            els.spot.classList.add('is-centered');
            els.spot.style.cssText = 'top:' + vh / 2 + 'px;left:' + vw / 2 + 'px;';
            els.pop.style.top = Math.max(16, (vh - ph) / 2) + 'px';
            els.pop.style.left = Math.max(16, (vw - pw) / 2) + 'px';
            return;
        }

        var r = current.getBoundingClientRect();
        els.spot.classList.remove('is-centered');
        els.spot.style.top = (r.top - pad) + 'px';
        els.spot.style.left = (r.left - pad) + 'px';
        els.spot.style.width = (r.width + pad * 2) + 'px';
        els.spot.style.height = (r.height + pad * 2) + 'px';

        // Prefer below, then above, then beside (right, then left) so the popover never covers the target.
        var top, left = r.left;
        var midTop = Math.min(Math.max(16, r.top + r.height / 2 - ph / 2), vh - ph - 16);
        if (r.bottom + pad + gap + ph <= vh - 16) top = r.bottom + pad + gap;
        else if (r.top - pad - gap - ph >= 16) top = r.top - pad - gap - ph;
        else if (r.right + pad + gap + pw <= vw - 16) { top = midTop; left = r.right + pad + gap; }
        else if (r.left - pad - gap - pw >= 16) { top = midTop; left = r.left - pad - gap - pw; }
        else top = vh - ph - 16; // no room anywhere (very large target on a phone): pin to the bottom
        left = Math.min(Math.max(16, left), vw - pw - 16);
        els.pop.style.top = Math.max(16, top) + 'px';
        els.pop.style.left = left + 'px';
    }

    function next() {
        var tour = tourById(state.tour);
        if (state.step >= tour.steps.length - 1) { finish(false); return; }
        state.step += 1;
        show();
    }

    function back() {
        if (state.step <= 0) return;
        state.step -= 1;
        var step = tourById(state.tour).steps[state.step];
        // Going back to a step on another page: navigate there if it has a fixed URL.
        if (!onPage(step) && step.url) { save(); location.href = step.url; return; }
        show();
    }

    function finish(skipped) {
        var tour = state && tourById(state.tour);
        teardown();
        clearSaved();
        if (tour && tour.overview) {
            body.setAttribute('data-guide-done', 'true');
            markCompleted(true);
        }
        state = null;
        if (!skipped && tour && tour.overview) {
            toast('That was the overview. Open the Guide (?) button any time for step-by-step task tours.');
        }
    }

    function toast(msg) {
        var c = document.getElementById('cp-toast-container');
        if (!c) return;
        var t = document.createElement('div');
        t.className = 'cp-toast cp-toast-success';
        t.textContent = msg;
        c.appendChild(t);
        setTimeout(function () { t.remove(); }, 6000);
    }

    function start(id, stepIndex) {
        if (!tourById(id)) return;
        closeMenu();
        state = { tour: id, step: stepIndex || 0 };
        show();
    }

    // ---- Header launcher menu ----
    var launcher = document.querySelector('[data-guide-launcher]');
    var menu = launcher ? launcher.parentElement.querySelector('.cp-guide-menu') : null;

    function closeMenu() { if (menu) { menu.hidden = true; launcher.setAttribute('aria-expanded', 'false'); } }

    if (launcher && menu) {
        var html = '<div class="cp-guide-menu-title">Guided tours</div>';
        tours.forEach(function (t) {
            html += '<button type="button" role="menuitem" data-tour="' + t.id + '">' + t.title + '<small>' + t.description + '</small></button>';
        });
        menu.innerHTML = html;
        menu.addEventListener('click', function (e) {
            var b = e.target.closest('button[data-tour]');
            if (b) start(b.getAttribute('data-tour'), 0);
        });
        launcher.addEventListener('click', function (e) {
            e.stopPropagation();
            var open = menu.hidden;
            menu.hidden = !open;
            launcher.setAttribute('aria-expanded', open ? 'true' : 'false');
            if (open) { var first = menu.querySelector('button'); if (first) first.focus(); }
        });
        document.addEventListener('click', function (e) { if (!menu.hidden && !menu.contains(e.target)) closeMenu(); });
        menu.addEventListener('keydown', function (e) { if (e.key === 'Escape') { closeMenu(); launcher.focus(); } });
    }

    // "Show the walkthrough again" (Profile page).
    document.addEventListener('click', function (e) {
        var b = e.target.closest('[data-guide-restart]');
        if (!b) return;
        e.preventDefault();
        markCompleted(false);
        var overview = tours.filter(function (t) { return t.overview; })[0];
        if (overview) start(overview.id, 0);
    });

    window.CraftGuide = { start: start, tours: tours };

    // ---- Resume a tour in progress, or auto-start the overview once per account ----
    var saved = load();
    if (saved && tourById(saved.tour)) {
        state = saved;
        show();
    } else if (body.getAttribute('data-guide-done') === 'false' && window.self === window.top) {
        var overview = tours.filter(function (t) { return t.overview; })[0];
        if (overview) setTimeout(function () { start(overview.id, 0); }, 400);
    }
})();
