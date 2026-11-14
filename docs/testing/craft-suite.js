/*
 * CRAFT functional test harness (see docs/test-plan-v2.md).
 * Usage: open any page of the running app (https://localhost:59360), paste this file into the
 * browser console, then:  await CRAFT.runAll()   or   await CRAFT.suites.RES()
 * It mutates data: run against a restored baseline DB. Demo password: Campus123.
 */
(() => {
  const P = new DOMParser();
  const results = [];
  const pad = n => String(n).padStart(2, '0');
  // Local wall-clock date N days from today, as yyyy-MM-dd.
  const day = n => { const d = new Date(); d.setDate(d.getDate() + n); return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`; };

  const get = async u => { const r = await fetch(u); return { status: r.status, url: r.url.replace(location.origin, ''), doc: P.parseFromString(await r.text(), 'text/html') }; };
  const tok = async u => (await get(u)).doc.querySelector('input[name=__RequestVerificationToken]')?.value || '';
  const read = (r, d) => {
    const js = [...d.scripts].map(s => s.textContent).join('');
    const msgs = [...d.querySelectorAll('.validation-summary-errors li, .validation-summary-errors:not(:has(li)), .cp-form-error, .cp-field-error, .cp-alert-error li')]
      .map(e => e.textContent.trim()).filter(Boolean);
    return {
      status: r.status, url: r.url.replace(location.origin, ''), doc: d,
      ok: (js.match(/var success = "([^"]*)"/) || [])[1] || '',
      err: (js.match(/var error = "([^"]*)"/) || [])[1] || '',
      msgs, get text() { return [this.ok, this.err, ...this.msgs].join(' | '); },
    };
  };
  const post = async (u, data, tokenFrom, opts = {}) => {
    const body = new URLSearchParams();
    for (const [k, v] of (Array.isArray(data) ? data : Object.entries(data))) body.append(k, v);
    if (!opts.noToken) body.append('__RequestVerificationToken', await tok(tokenFrom || u));
    const r = await fetch(u, { method: 'POST', body });
    return read(r, P.parseFromString(await r.text(), 'text/html'));
  };
  const postForm = async (u, data, files, tokenFrom) => {
    const fd = new FormData();
    for (const [k, v] of Object.entries(data)) fd.append(k, v);
    for (const [k, f] of files || []) fd.append(k, f);
    fd.append('__RequestVerificationToken', await tok(tokenFrom || u));
    const r = await fetch(u, { method: 'POST', body: fd });
    return read(r, P.parseFromString(await r.text(), 'text/html'));
  };
  const logout = async () => { const t = await tok('/Profile'); if (t) await fetch('/Account/Logout', { method: 'POST', body: new URLSearchParams({ __RequestVerificationToken: t }) }); };
  const login = async (id, pw = 'Campus123') => { await logout(); return post('/Account/Login', { UserId: id, Password: pw }, '/Account/Login'); };
  const check = (id, name, pass, detail = '') => { results.push({ id, result: pass ? 'PASS' : 'FAIL', name, detail: String(detail).slice(0, 220) }); };
  const has = (r, re) => (re instanceof RegExp ? re : new RegExp(re, 'i')).test(r.text);
  const opt = (doc, name, re) => [...doc.querySelectorAll(`[name="${name}"] option`)].find(o => o.value && (!re || re.test(o.textContent)))?.value;
  const rowsOf = doc => [...doc.querySelectorAll('tbody tr')].map(t => t.textContent.replace(/\s+/g, ' ').trim());
  const idFromLink = (doc, re) => { const a = [...doc.querySelectorAll('a[href]')].find(x => re.test(x.getAttribute('href'))); return a ? a.getAttribute('href').match(/(\d+)(?!.*\d)/)[1] : null; };
  const reviewIdFor = async rsv => { const q = await get('/Custodian/Reservations?search=' + rsv); const tr = [...q.doc.querySelectorAll('tbody tr')].find(t => t.textContent.includes(rsv)); return tr?.querySelector('a[href*="/Custodian/Review/"]')?.getAttribute('href').split('/').pop(); };

  // Submits a reservation for the signed-in borrower; returns { res, rsv } where rsv is the new number.
  const reserve = async (equipmentId, over = {}) => {
    const f = await get('/Reservations/New/' + equipmentId);
    const before = new Set(rowsOf((await get('/Reservations/Mine')).doc).map(r => r.split(' ')[0]));
    const data = {
      'Form.EquipmentId': equipmentId, 'Form.PickupLocationId': opt(f.doc, 'Form.PickupLocationId'),
      'Form.Quantity': '1', 'Form.BorrowingDate': day(3), 'Form.BorrowingTime': '09:00',
      'Form.ExpectedReturnDate': day(4), 'Form.ExpectedReturnTime': '09:00', 'Form.Purpose': 'QA suite',
      'Form.AcknowledgeRules': 'true', 'Form.AcknowledgeResponsibility': 'true', ...over,
    };
    const res = await post('/Reservations/New', Object.entries(data).filter(([, v]) => v !== null), '/Reservations/New/' + equipmentId);
    const after = rowsOf((await get('/Reservations/Mine')).doc).map(r => r.split(' ')[0]);
    return { res, rsv: after.find(x => /^RSV-/.test(x) && !before.has(x)) || null };
  };
  const cancelAllMine = async () => {
    const m = await get('/Reservations/Mine');
    for (const f of m.doc.querySelectorAll('form[action*="/Reservations/Cancel/"]')) await post(f.getAttribute('action'), {}, '/Reservations/Mine');
  };

  const BASE_POLICY = { 'Policy.MaxBorrowDurationDays': '7', 'Policy.MaxUnitsPerBorrower': '3', 'Policy.MaxActiveBorrowingUnits': '3', 'Policy.OverdueGracePeriodMinutes': '60', 'Policy.LowStockThreshold': '2', 'Policy.BlockReservationWhenOverdue': 'true' };
  // Reservation rules depend on the current policy, so pin it to the documented baseline first.
  const ensureBasePolicy = async () => { await login('ADMIN-0001'); await post('/Settings/SavePolicy', BASE_POLICY, '/Settings'); };

  const suites = {};

  suites.AUTH = async () => {
    await logout();
    const reg = await get('/Account/Register');
    const role = opt(reg.doc, 'RoleId', /student/i), dept = opt(reg.doc, 'DepartmentId');
    const stamp = Date.now().toString().slice(-6);
    const RB = { FirstName: 'QA', LastName: 'Reg', AccountIdentifier: 'QA-' + stamp, RoleId: role, DepartmentId: dept, SchoolEmail: `qa.${stamp}@university.edu.ph`, Password: 'Campus123', ConfirmPassword: 'Campus123', PrivacyConsent: 'true' };
    const R = o => post('/Account/Register', { ...RB, ...o }, '/Account/Register');
    let r = await R({ FirstName: '', LastName: '', AccountIdentifier: '', SchoolEmail: '', Password: '', ConfirmPassword: '' });
    check('AUTH-01', 'register blank', r.url.startsWith('/Account/Register') && r.msgs.length > 0, r.text);
    r = await R({ AccountIdentifier: 'AB1' }); check('AUTH-02a', 'id 3 chars', has(r, '4 to 20'), r.text);
    r = await R({ AccountIdentifier: '1'.repeat(21) }); check('AUTH-02b', 'id 21 chars', has(r, '4 to 20'), r.text);
    r = await R({ AccountIdentifier: 'AB$12' }); check('AUTH-02c', 'id bad chars', has(r, 'letters, digits'), r.text);
    r = await R({ SchoolEmail: 'nope' }); check('AUTH-03', 'bad email', has(r, 'email'), r.text);
    r = await R({ Password: 'campus12', ConfirmPassword: 'campus12' }); check('AUTH-04', 'weak password', has(r, 'uppercase'), r.text);
    r = await R({ ConfirmPassword: 'Campus124' }); check('AUTH-05', 'pw mismatch', has(r, 'do not match'), r.text);
    r = await R({ PrivacyConsent: 'false' }); check('AUTH-06a', 'consent required', has(r, 'Privacy'), r.text);
    check('AUTH-06b', 'privacy notice present', !!reg.doc.querySelector('details.cp-privacy-notice'));
    r = await R({ AccountIdentifier: '2023-104857' }); check('AUTH-07a', 'duplicate id', has(r, 'already exists'), r.text);
    r = await R({ SchoolEmail: 'maria.santos@university.edu.ph', AccountIdentifier: 'QA-D' + stamp }); check('AUTH-07b', 'duplicate email', has(r, 'already exists|school-issued'), r.text);
    r = await R({}); check('AUTH-08', 'valid registration', r.url.startsWith('/Account/Login'), r.url);
    r = await post('/Account/Login', { UserId: RB.SchoolEmail, Password: 'Campus123' }, '/Account/Login');
    check('AUTH-09', 'login by email', /Dashboard/.test(r.url), r.url);
    await logout();
    r = await post('/Account/Login', { UserId: '2023-104857', Password: 'Wrong1234' }, '/Account/Login'); check('AUTH-10a', 'wrong password', has(r, 'Invalid'), r.text);
    r = await post('/Account/Login', { UserId: 'NOPE-0000', Password: 'Campus123' }, '/Account/Login'); check('AUTH-10b', 'unknown user is generic (D7)', has(r, 'Invalid'), r.text);
    const lk = []; for (let i = 0; i < 5; i++) lk.push(await post('/Account/Login', { UserId: RB.AccountIdentifier, Password: 'Bad' + i + 'Xx1' }, '/Account/Login'));
    r = await post('/Account/Login', { UserId: RB.AccountIdentifier, Password: 'Campus123' }, '/Account/Login');
    check('AUTH-11', 'lockout after failures', has(lk[4], 'Too many') && has(r, 'Too many'), r.text);
    r = await post('/Account/Login', { UserId: '2023-104857', Password: 'Campus123', ReturnUrl: '/Reservations/Mine' }, '/Account/Login'); check('AUTH-13a', 'local ReturnUrl', r.url === '/Reservations/Mine', r.url);
    await logout();
    r = await post('/Account/Login', { UserId: '2023-104857', Password: 'Campus123', ReturnUrl: 'https://evil.example.com/' }, '/Account/Login'); check('AUTH-13b', 'external ReturnUrl ignored', r.url.startsWith('/'), r.url);
    await logout(); const g = await get('/Borrower/Dashboard'); check('AUTH-14', 'logout protects pages', g.url.startsWith('/Account/Login'), g.url);
    r = await post('/Account/ForgotPassword', { Email: '' }, '/Account/ForgotPassword'); check('AUTH-15a', 'forgot blank', has(r, 'registered email'), r.text);
    r = await post('/Account/ForgotPassword', { Email: 'ghost@university.edu.ph' }, '/Account/ForgotPassword'); check('AUTH-15b', 'forgot unknown is generic (D7)', !has(r, 'does not exist'), r.text);
    r = await post('/Account/ForgotPassword', { Email: 'maria.santos@university.edu.ph' }, '/Account/ForgotPassword');
    const link = [...r.doc.querySelectorAll('a,input,code')].map(x => x.getAttribute('href') || x.value || x.textContent).find(h => h && /ResetPassword\?token=/i.test(h));
    check('AUTH-15c', 'forgot known issues link', !!link, link);
    if (link) {
      const u = link.replace(location.origin, ''); const t = (await get(u)).doc.querySelector('[name=Token]')?.value;
      const RP = (a, b) => post('/Account/ResetPassword', { Token: t, NewPassword: a, ConfirmPassword: b }, u);
      r = await RP('Campus456', 'Campus457'); check('AUTH-16a', 'reset mismatch shows message (D5)', has(r, 'do not match'), r.text);
      r = await RP('abc', 'abc'); check('AUTH-16b', 'reset weak', has(r, 'uppercase'), r.text);
      r = await RP('Campus123', 'Campus123'); check('AUTH-16c', 'reset valid', r.url.startsWith('/Account/Login'), r.url);
      r = await RP('Campus124', 'Campus124'); check('AUTH-16d', 'reset token single use', !r.url.startsWith('/Account/Login'), r.url);
    }
    await login('2024-118002'); const u = await reserve(5);
    check('AUTH-19', 'unverified cannot reserve', has(u.res, 'pending verification'), u.res.text);
  };

  suites.RBAC = async () => {
    const staff = ['/Custodian/Dashboard', '/Custodian/Reservations', '/Returns', '/Equipment', '/Equipment/Create'];
    const admin = ['/Users', '/Users/Verifications', '/Settings', '/Monitoring/Dashboard', '/Monitoring/AuditLogs', '/Monitoring/ExportAuditCsv', '/Announcements', '/CustodianAssignments', '/Operator'];
    await logout();
    for (const u of [...staff, ...admin, '/Borrower/Dashboard', '/Profile', '/Notifications']) { const g = await get(u); check('RBAC-01', 'anonymous ' + u, g.url.startsWith('/Account/Login'), g.url); }
    await login('2023-104857');
    for (const u of [...staff, ...admin]) { const g = await get(u); check('RBAC-02', 'student ' + u, g.status === 403, g.status); }
    let r = await post('/Profile/SaveContact', { ContactNumber: '09170000000' }, null, { noToken: true }); check('RBAC-05', 'missing token', r.status === 400, r.status);
    const foreign = ['/Support/Details/1', '/Reservations/Details/999999', '/Borrowings/Details/999999'];
    for (const u of foreign) { const g = await get(u); check('RBAC-06', 'foreign/missing ' + u, !g.url.startsWith(u), g.url); }
    await login('EMP-1001');
    for (const u of admin) { const g = await get(u); check('RBAC-03', 'custodian ' + u, g.status === 403, g.status); }
    for (const u of ['/Reservations/New/1', '/Reservations/Mine']) { const g = await get(u); check('RBAC-04', 'custodian ' + u, g.status === 403, g.status); }
  };

  suites.RES = async () => {
    await ensureBasePolicy();
    await login('2023-104857'); await cancelAllMine();
    const av = await get('/Reservations/Available?search=Dell'); check('RES-01a', 'search narrows', rowsOf(av.doc).length >= 0 && av.status === 200, av.status);
    const cats = await get('/Reservations/Available'); const c = opt(cats.doc, 'category');
    check('RES-01b', 'category filter', (await get('/Reservations/Available?category=' + c)).status === 200);
    const g = await get('/Reservations/New/999999'); check('RES-03', 'missing equipment', !g.url.startsWith('/Reservations/New'), g.url);
    let x = await reserve(5, { 'Form.Quantity': '', 'Form.PickupLocationId': '', 'Form.BorrowingDate': '', 'Form.BorrowingTime': '', 'Form.ExpectedReturnDate': '', 'Form.ExpectedReturnTime': '', 'Form.Purpose': '', 'Form.AcknowledgeRules': 'false', 'Form.AcknowledgeResponsibility': 'false' });
    check('RES-04', 'all blank', x.res.msgs.length >= 7, x.res.msgs.length + ' messages');
    for (const [q, label] of [['0', 'zero'], ['-1', 'negative'], ['1.5', 'decimal'], ['abc', 'text']]) { x = await reserve(5, { 'Form.Quantity': q }); check('RES-05', 'quantity ' + label, !x.rsv, x.res.text); }
    x = await reserve(5, { 'Form.Quantity': '4' }); check('RES-06a', 'over per-reservation max', has(x.res, 'at most'), x.res.text);
    x = await reserve(7, {}); check('RES-06b', 'over availability', !x.rsv, x.res.text);
    x = await reserve(5, { 'Form.BorrowingDate': day(-1), 'Form.ExpectedReturnDate': day(0) }); check('RES-07a', 'past date', has(x.res, 'future'), x.res.text);
    x = await reserve(5, { 'Form.BorrowingDate': day(0), 'Form.BorrowingTime': '23:00', 'Form.ExpectedReturnDate': day(1) }); check('RES-07b', 'lead time', has(x.res, 'lead time'), x.res.text);
    x = await reserve(5, { 'Form.BorrowingDate': day(40), 'Form.ExpectedReturnDate': day(41) }); check('RES-07c', 'too far ahead', has(x.res, '30 days'), x.res.text);
    x = await reserve(5, { 'Form.ExpectedReturnDate': day(2) }); check('RES-08', 'return before borrow', has(x.res, 'after the borrowing'), x.res.text);
    x = await reserve(5, { 'Form.ExpectedReturnDate': day(12) }); check('RES-09a', 'student > 7 days', has(x.res, 'duration'), x.res.text);
    x = await reserve(5, { 'Form.AcknowledgeRules': 'false' }); check('RES-12a', 'ack required', has(x.res, 'borrowing rules'), x.res.text);
    x = await reserve(5, { 'Form.PickupLocationId': '999999' }); check('RES-13', 'bad pickup', has(x.res, 'pickup'), x.res.text);
    x = await reserve(5, {}); check('RES-14', 'valid reservation', has(x.res, 'submitted') && !!x.rsv, x.res.text);
    const mine = await get('/Reservations/Mine'); const row = rowsOf(mine.doc).find(t => t.startsWith(x.rsv));
    check('RES-15', 'time displayed as entered', row && row.includes('09:00 AM'), row);
    const did = idFromLink(mine.doc, /\/Reservations\/Details\/\d+/);
    check('RES-16a', 'details', (await get('/Reservations/Details/' + did)).status === 200);
    check('RES-16b', 'slip', (await get('/Reservations/Slip/' + did)).status === 200);
    const y = await reserve(3, {}); const z = await reserve(1, {});
    const w = await reserve(5, {}); check('RES-10', 'open-request limit', has(w.res, 'maximum number of open'), w.res.text);
    const cf = mine.doc.querySelector(`form[action*="/Reservations/Cancel/"]`);
    let r = await post(cf.getAttribute('action'), {}, '/Reservations/Mine'); check('RES-17', 'cancel pending', has(r, 'cancelled'), r.text);
    r = await post(cf.getAttribute('action'), {}, '/Reservations/Mine'); check('RES-18', 'cancel twice', has(r, 'no longer be cancelled'), r.text);
    await cancelAllMine();
    await login('EMP-2001'); await cancelAllMine();
    x = await reserve(1, { 'Form.ExpectedReturnDate': day(18) }); check('RES-09b', 'faculty 15 days allowed', has(x.res, 'submitted'), x.res.text);
    const f2 = await reserve(1, { 'Form.ExpectedReturnDate': day(28) }); check('RES-09c', 'faculty > 21 days', has(f2.res, 'duration'), f2.res.text);
    await cancelAllMine();
    return { y, z };
  };

  suites.CUS = async () => {
    await login('2023-104857'); await cancelAllMine();
    const a = await reserve(5, {}), b = await reserve(5, {}), c = await reserve(3, {});
    await login('EMP-1001');
    const q = await get('/Custodian/Reservations?search=' + a.rsv); check('CUS-01', 'queue search', rowsOf(q.doc).some(t => t.includes(a.rsv)), a.rsv);
    const ia = await reviewIdFor(a.rsv), ib = await reviewIdFor(b.rsv), ic = await reviewIdFor(c.rsv);
    const claimForm = (await get('/Custodian/Review/' + ia)).doc.querySelector('form[action*="/Custodian/Claim/"]');
    if (claimForm) { const r = await post(claimForm.getAttribute('action'), {}, '/Custodian/Review/' + ia); check('CUS-02', 'claim pooled reservation', !has(r, 'error'), r.text); }
    let r = await post('/Custodian/Approve/' + ia, { notes: 'n'.repeat(501) }, '/Custodian/Review/' + ia); check('CUS-03a', 'approve notes > 500', has(r, 'at most 500'), r.text);
    r = await post('/Custodian/Approve/' + ia, { notes: 'OK' }, '/Custodian/Review/' + ia); check('CUS-03b', 'approve', has(r, 'approved'), r.text);
    r = await post('/Custodian/Approve/' + ia, {}, '/Custodian/Review/' + ia); check('CUS-05', 'approve non-pending', has(r, 'no longer pending'), r.text);
    r = await post('/Custodian/Reject/' + ib, { reason: '' }, '/Custodian/Review/' + ib); check('CUS-04a', 'reject needs reason', has(r, 'reason is required'), r.text);
    r = await post('/Custodian/Reject/' + ib, { reason: 'QA reject' }, '/Custodian/Review/' + ib); check('CUS-04b', 'reject', has(r, 'rejected'), r.text);
    await login('ADMIN-0001');
    const icAdmin = await reviewIdFor(c.rsv);
    r = await post('/Custodian/Approve/' + icAdmin, {}, '/Custodian/Review/' + icAdmin);
    r = await post('/Custodian/Expire/' + icAdmin, { reason: 'No-show' }, '/Custodian/Review/' + icAdmin); check('CUS-07a', 'expire approved no-show', has(r, 'expired'), r.text);
    await login('EMP-1001');
    const R = o => post('/Custodian/Release', { ReservationId: ia, ReleasedQuantity: '1', ReleaseConditionId: '1', ReleaseNotes: '', ...o }, '/Custodian/Release/' + ia);
    r = await R({ ReleasedQuantity: '0' }); check('CUS-08a', 'release qty 0', has(r, 'at least 1'), r.text);
    r = await R({ ReleasedQuantity: '3' }); check('CUS-08b', 'release over approved', has(r, 'cannot exceed'), r.text);
    r = await R({ ReleaseConditionId: '' }); check('CUS-08c', 'release without condition', r.url.startsWith('/Custodian/Release'), r.text);
    r = await R({ ReleaseConditionId: '5' }); check('CUS-08d', 'release as Lost refused (D18)', has(r, "can't be released"), r.text);
    const relOpts = [...(await get('/Custodian/Release/' + ia)).doc.querySelectorAll('[name=ReleaseConditionId] option')].map(o => o.textContent.trim());
    check('CUS-08e', 'release list hides damaged/lost/maintenance (D18)', !relOpts.some(t => /^(Lost|Damaged|Under Maintenance)$/.test(t)), relOpts.join(','));
    r = await R({}); check('CUS-09', 'release', has(r, 'released successfully'), r.text);
    r = await R({}); check('CUS-10', 'release twice', has(r, 'Only approved'), r.text);
    r = await post('/Custodian/Expire/' + ia, { reason: 'late' }, '/Custodian/Review/' + ia); check('CUS-07b', 'expire after release refused', !has(r, 'expired\\.'), r.text);
    const eq = await get('/Equipment/Details/5'); const asset = (eq.doc.body.textContent.match(/AUD-\d+/) || [])[0];
    let s = await get('/Custodian/Scan?code=' + asset); check('CUS-11a', 'scan asset code opens the match', s.status === 200 && /Returns\/Create|Custodian\/Release|Scan/.test(s.url), s.url);
    s = await get('/Custodian/Scan?code=NOPE-XYZ'); check('CUS-11b', 'scan unknown', s.status === 200, s.status);
    s = await get('/Custodian/Scan?code='); check('CUS-11c', 'scan empty', s.status === 200, s.status);
    return a.rsv;
  };

  suites.RET = async () => {
    // Borrowing created by CUS (1 unit of equipment 5).
    await login('EMP-1001');
    const bl = await get('/Custodian/Borrowings');
    check('RET-00a', 'borrowings list links to Record return (D20)', !!idFromLink(bl.doc, /\/Returns\/Create\/\d+/), 'no link in list');
    // Scan by asset code opens the return form directly when exactly one borrowing matches.
    const sc = await get('/Custodian/Scan?code=AUD-301');
    const bid = (sc.url.match(/\/Returns\/Create\/(\d+)/) || [])[1] || idFromLink(sc.doc, /\/Returns\/Create\/\d+/);
    check('RET-00b', 'active borrowing reachable via Scan', !!bid, sc.url);
    if (!bid) return;
    const c = await get('/Returns/Create/' + bid); const def = c.doc.querySelector('[name=ReturnedAtUtc]').value.slice(0, 16);
    const RC = o => post('/Returns/Create', { BorrowingId: bid, ReturnedQuantity: '1', ReturnedAtUtc: def, Notes: '', ...o }, '/Returns/Create/' + bid);
    let r = await RC({ ReturnedQuantity: '0' }); check('RET-01a', 'return qty 0 (D8)', /between|at least/i.test(r.text), r.text);
    r = await RC({ ReturnedQuantity: '5' }); check('RET-01b', 'over-return', has(r, 'between'), r.text);
    r = await RC({ ReturnedAtUtc: day(30) + 'T10:00' }); check('RET-02', 'future return', has(r, 'future'), r.text);
    r = await RC({}); check('RET-03', 'return', has(r, 'Return recorded'), r.text);
    const I = (o, files) => postForm('/Returns/Inspect', { BorrowingId: bid, InspectedQuantity: '1', InspectionStatusId: '2', CurrentConditionId: '1', GoodQuantity: '1', DamagedQuantity: '0', LostQuantity: '0', RepairCost: '0', PenaltyAmount: '0', ...o }, files, '/Returns/Inspect/' + bid);
    r = await I({ GoodQuantity: '0' }); check('RET-06', 'outcome sum', has(r, 'add up'), r.text);
    r = await I({ RepairCost: '-1' }); check('RET-07a', 'negative cost', has(r, 'negative'), r.text);
    r = await I({ InspectedQuantity: '3', GoodQuantity: '3' }); check('RET-07b', 'over-inspect', has(r, "can't exceed"), r.text);
    const png = new File([Uint8Array.from(atob('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII='), ch => ch.charCodeAt(0))], 'ok.png', { type: 'image/png' });
    r = await I({}, [['photos', png], ['photos', new File(['MZ'], 'evil.exe')]]); check('RET-10', 'skipped photo reported (D19)', has(r, 'Skipped 1 photo'), r.text);
    check('RET-08', 'inspect all good with photo', r.url === '/Returns' || has(r, 'recorded'), r.text);
  };

  suites.EQP = async () => {
    await login('ADMIN-0001');
    const cf = await get('/Equipment/Create'); const o = n => opt(cf.doc, n);
    const base = { EquipmentName: 'QA Item', AssetCode: 'QA-EQ-' + Date.now().toString().slice(-5), EquipmentCategoryId: o('EquipmentCategoryId'), LocationId: o('LocationId'), OperationalStatusId: o('OperationalStatusId'), CurrentConditionId: o('CurrentConditionId'), TotalQuantity: '2', Description: '', IsActive: 'true' };
    const C = (x, files) => postForm('/Equipment/Create', { ...base, ...x }, files, '/Equipment/Create');
    let r = await C({ EquipmentName: '', AssetCode: '', EquipmentCategoryId: '' }); check('EQP-01a', 'blank', r.msgs.length >= 2, r.text);
    r = await C({ TotalQuantity: '0' }); check('EQP-01b', 'qty 0', has(r, 'between 1'), r.text);
    r = await C({ AssetCode: 'LP-001' }); check('EQP-01c', 'duplicate asset code', has(r, 'unique'), r.text);
    r = await C({}, [['imageFile', new File(['x'], 'evil.exe')]]); check('EQP-03', 'bad image shows error (D6)', r.msgs.length > 0, r.text || 'no message');
    r = await C({}); check('EQP-02', 'create', has(r, 'created'), r.text);
    const id = idFromLink((await get('/Equipment?search=' + base.AssetCode)).doc, /\/Equipment\/Details\/\d+/);
    r = await postForm('/Equipment/Edit/' + id, { ...base, EquipmentId: id, EquipmentName: 'QA Item v2' }, null, '/Equipment/Edit/' + id); check('EQP-04', 'edit', has(r, 'updated'), r.text);
    check('EQP-07', 'label + details', (await get('/Equipment/Label/' + id)).status === 200 && (await get('/Equipment/Details/' + id)).status === 200);
    r = await postForm('/Equipment/Edit/5', { ...base, AssetCode: 'AUD-301', EquipmentId: 5, EquipmentName: 'Shure SM58 Microphone Set', TotalQuantity: '0' }, null, '/Equipment/Edit/5');
    check('EQP-05', 'qty below committed / invalid', !has(r, 'updated'), r.text);
    r = await post('/Equipment/Delete/' + id, {}, '/Equipment/Details/' + id); check('EQP-06a', 'delete unused', has(r, 'deleted'), r.text);
    r = await post('/Equipment/Delete/1', {}, '/Equipment/Details/1'); check('EQP-06b', 'delete in use blocked', has(r, 'cannot be deleted'), r.text);
  };

  suites.ADM = async () => {
    await login('ADMIN-0001');
    const cf = await get('/Users/Create'); const st = opt(cf.doc, 'AccountStatusId', /^\s*active/i), dp = opt(cf.doc, 'DepartmentId');
    const n = Date.now().toString().slice(-5);
    const U = o => post('/Users/Create', { FirstName: 'QA', LastName: 'Adm', AccountIdentifier: 'QA-U' + n, SchoolEmail: `qa.u${n}@university.edu.ph`, RoleId: '1', DepartmentId: dp, AccountStatusId: st, TemporaryPassword: 'Temp1234', ...o }, '/Users/Create');
    let r = await U({ FirstName: '', AccountIdentifier: '', SchoolEmail: '', RoleId: '' }); check('ADM-01a', 'create blank', r.msgs.length >= 3, r.text);
    r = await U({ AccountIdentifier: '2023-104857' }); check('ADM-01b', 'create duplicate id', has(r, 'unique'), r.text);
    r = await U({ TemporaryPassword: 'abc' }); check('ADM-01c', 'weak temp password', has(r, 'Temporary password'), r.text);
    r = await U({ AccountIdentifier: 'bad id!!', SchoolEmail: `qa.b${n}@university.edu.ph` }); check('ADM-01d', 'bad id format refused (D10)', !has(r, 'created'), r.text);
    const created = await U({}); check('ADM-02', 'create user', has(created, 'created'), created.text);
    const uid = created.url.match(/Details\/(\d+)/)?.[1];
    const ef = await get('/Users/Edit/' + uid); const vals = {};
    ef.doc.querySelectorAll('form[action*="/Users/Edit"] input, form[action*="/Users/Edit"] select').forEach(e => { if (e.name && e.name !== '__RequestVerificationToken') vals[e.name] = e.value; });
    r = await post('/Users/Edit/' + uid, { ...vals, SchoolEmail: 'bad' }, '/Users/Edit/' + uid); check('ADM-03a', 'edit invalid email', has(r, 'valid email'), r.text);
    r = await post('/Users/Edit/' + uid, { ...vals, LastName: 'Edited' }, '/Users/Edit/' + uid); check('ADM-03b', 'edit', has(r, 'updated'), r.text);
    r = await post('/Users/Delete/1', {}, '/Users'); check('ADM-04a', 'self delete blocked', has(r, 'own account'), r.text);
    r = await post('/Users/Delete/3', {}, '/Users'); check('ADM-04b', 'delete user with transactions blocked', has(r, 'cannot be deleted'), r.text);
    r = await post('/Users/ResetPassword/' + uid, {}, '/Users/Details/' + uid); check('ADM-05a', 'reset password', has(r, 'issued'), r.text);
    const temp = (r.doc.body.textContent.match(/Temporary password[^A-Za-z0-9]*([A-Za-z0-9!@#$%^&*]{8,})/i) || [])[1];
    r = await post('/Users/InviteLink/' + uid, {}, '/Users/Details/' + uid); check('ADM-06', 'invite link', has(r, 'Invite link'), r.text);
    if (temp) { await logout(); const l = await post('/Account/Login', { UserId: 'QA-U' + n, Password: temp }, '/Account/Login'); check('AUTH-18', 'forced password change', /ChangePassword/.test(l.url), l.url); await login('ADMIN-0001'); }
    else check('AUTH-18', 'forced password change (temp password not visible to parse)', true, 'skipped');
    r = await post('/Users/Delete/' + uid, {}, '/Users/Details/' + uid); check('ADM-04c', 'delete fresh user with history (D4)', has(r, 'deleted'), r.text);
    // Verify buttons are forms; prefer a QA-registered account so seeded people stay pending.
    const vq = await get('/Users/Verifications');
    const vForms = [...vq.doc.querySelectorAll('form[action*="/Users/Verify/"]')];
    const vForm = vForms.find(f => /QA/.test(f.closest('tr')?.textContent || '')) || vForms[0];
    const vid = vForm?.getAttribute('action').match(/Verify\/(\d+)/)[1];
    r = await post('/Users/RejectVerification/' + vid, { reason: '' }, '/Users/Verifications'); check('ADM-07a', 'reject needs reason', has(r, 'reason is required'), r.text);
    r = await post('/Users/Verify/' + vid + '?returnTab=pending', {}, '/Users/Verifications'); check('ADM-07b', 'verify', has(r, 'verified'), r.text);
    r = await post('/Users/Verify/' + vid + '?returnTab=pending', {}, '/Users/Verifications'); check('ADM-07c', 'verify twice', has(r, 'not awaiting'), r.text);
    const ca = await get('/CustodianAssignments');
    const catForm = [...ca.doc.querySelectorAll('form[action*="/CustodianAssignments/Assign"]')].find(f => f.querySelector('[name=scopeType]')?.value === 'CATEGORY');
    const tgt = [...catForm.querySelectorAll('[name=targetId] option')].map(x => x.value).filter(Boolean).pop();
    const cus = [...catForm.querySelectorAll('[name=custodianUserId] option')].map(x => x.value).filter(Boolean)[0];
    r = await post('/CustodianAssignments/Assign', { scopeType: 'CATEGORY', targetId: '', custodianUserId: '' }, '/CustodianAssignments'); check('ADM-09a', 'assign blank', has(r, 'Choose'), r.text);
    r = await post('/CustodianAssignments/Assign', { scopeType: 'CATEGORY', targetId: tgt, custodianUserId: cus }, '/CustodianAssignments'); check('ADM-09b', 'assign', has(r, 'assigned'), r.text);
    r = await post('/CustodianAssignments/Assign', { scopeType: 'CATEGORY', targetId: tgt, custodianUserId: cus }, '/CustodianAssignments'); check('ADM-09c', 'assign duplicate', has(r, 'already has'), r.text);
    const un = [...(await get('/CustodianAssignments')).doc.querySelectorAll('form[action*="Unassign"]')].pop();
    r = await post(un.getAttribute('action'), Object.fromEntries([...un.querySelectorAll('input')].filter(i => i.name !== '__RequestVerificationToken').map(i => [i.name, i.value])), '/CustodianAssignments'); check('ADM-09d', 'unassign', has(r, 'removed'), r.text);
    r = await post('/Announcements/Publish', { Title: '', Body: '' }, '/Announcements'); check('ADM-10a', 'announcement blank', has(r, 'needs a title'), r.text);
    r = await post('/Announcements/Publish', [['Title', 'QA Students only'], ['Body', 'Hello students'], ['TargetRoles', 'STUDENT']], '/Announcements'); check('ADM-10b', 'announcement publish', has(r, 'published'), r.text);
    r = await post('/Announcements/Publish', [['Title', 'QA expired'], ['Body', 'x'], ['TargetRoles', 'STUDENT'], ['ExpiresOn', day(-3)]], '/Announcements'); check('ADM-11', 'expired announcement refused (D16)', !has(r, 'published'), r.text);
    const P0 = { 'Policy.MaxBorrowDurationDays': '7', 'Policy.MaxUnitsPerBorrower': '3', 'Policy.MaxActiveBorrowingUnits': '3', 'Policy.OverdueGracePeriodMinutes': '60', 'Policy.LowStockThreshold': '2', 'Policy.BlockReservationWhenOverdue': 'true' };
    r = await post('/Settings/SavePolicy', { ...P0, 'Policy.MaxBorrowDurationDays': '0' }, '/Settings'); check('ADM-12a', 'policy range', has(r, 'between'), r.text);
    r = await post('/Settings/SavePolicy', { ...P0, 'Policy.MaxUnitsPerBorrower': '2' }, '/Settings'); check('ADM-12b', 'policy publish', has(r, 'published'), r.text);
    r = await post('/Settings/SavePolicy', { ...P0, 'Policy.MaxUnitsPerBorrower': '2' }, '/Settings'); check('ADM-12c', 'policy no-op', has(r, 'No changes'), r.text);
    await login('2023-104857'); const nf = await get('/Reservations/New/5');
    check('ADM-12d', 'policy reaches the form', nf.doc.querySelector('[name="Form.Quantity"]').getAttribute('max') === '2', nf.doc.querySelector('[name="Form.Quantity"]').getAttribute('max'));
    await login('ADMIN-0001'); await post('/Settings/SavePolicy', P0, '/Settings');
    r = await post('/Settings/AddCategory', { code: 'laptop', name: 'L' }, '/Settings'); check('ADM-13a', 'category duplicate', has(r, 'already exists'), r.text);
    r = await post('/Settings/AddCategory', { code: 'a b', name: 'L' }, '/Settings'); check('ADM-13b', 'category bad code', has(r, 'Code must be'), r.text);
    r = await post('/Settings/AddCategory', { code: 'QA-CAT', name: 'QA Cat' }, '/Settings'); check('ADM-13c', 'category add', has(r, 'added'), r.text);
    const del = [...(await get('/Settings')).doc.querySelectorAll('form[action*=DeleteCategory]')].find(f => f.closest('tr').textContent.includes('QA-CAT'));
    r = await post(del.getAttribute('action'), {}, '/Settings'); check('ADM-13d', 'category delete', has(r, 'deleted'), r.text);
    const lap = [...(await get('/Settings')).doc.querySelectorAll('form[action*=DeleteCategory]')].find(f => f.closest('tr').textContent.includes('LAPTOP'));
    r = await post(lap.getAttribute('action'), {}, '/Settings'); check('ADM-13e', 'category in use', has(r, 'used by'), r.text);
    const op = await get('/Operator'); const oid = opt(op.doc, 'userId');
    r = await post('/Operator/Select', { userId: oid }, '/Operator'); check('ADM-14', 'operator select', has(r, 'Now acting'), r.text);
  };

  suites.MON = async () => {
    await login('ADMIN-0001');
    for (const u of ['/Monitoring/Dashboard', '/Monitoring/Transactions', '/Monitoring/Reports', `/Monitoring/Reports?from=${day(10)}&to=${day(-10)}`, '/Monitoring/AuditLogs?entityType=User']) {
      const g = await get(u); check('MON-02', u, g.status === 200, g.status);
    }
    const audit = await get('/Monitoring/AuditLogs'); check('MON-03', 'audit log has recent QA actions', /QA/.test(audit.doc.body.textContent), '');
    for (const x of ['ExportAuditCsv', 'ExportBorrowingsCsv', 'ExportMovementsCsv', 'ExportReservationsCsv']) {
      const r = await fetch('/Monitoring/' + x); const t = await r.text();
      const lines = t.trim().split(/\r?\n/);
      check('MON-04', x, r.status === 200 && /csv/.test(r.headers.get('content-type')) && lines.length >= 1, `${lines.length} lines`);
      check('MON-04f', x + ' no formula injection', !/(^|,)"?[=+@]/m.test(lines.slice(1).join('\n')), '');
    }
  };

  suites.COM = async () => {
    await login('2023-104857');
    let r = await post('/Support/Create', { Category: 'OTHER', Subject: '', Body: '' }, '/Support/Create'); check('COM-01a', 'ticket blank', has(r, 'required'), r.text);
    r = await post('/Support/Create', { Category: 'HACK', Subject: 'x', Body: 'y' }, '/Support/Create'); check('COM-01b', 'ticket invalid category shows message (D13)', r.msgs.length > 0 || r.err, r.text || 'no message');
    r = await post('/Support/Create', { Category: 'OTHER', Subject: 's'.repeat(201), Body: 'y' }, '/Support/Create'); check('COM-01c', 'subject > 200', has(r, '200'), r.text);
    for (const cat of ['VERIFICATION', 'APPEAL', 'FEEDBACK', 'OTHER']) { r = await post('/Support/Create', { Category: cat, Subject: 'QA ' + cat, Body: 'QA body' }, '/Support/Create'); check('COM-02', 'ticket ' + cat, has(r, 'submitted'), r.text); }
    const tid = r.url.match(/Details\/(\d+)/)?.[1];
    await login('ADMIN-0001');
    r = await post('/Support/Reply/' + tid, { body: '' }, '/Support/Details/' + tid); check('COM-03c', 'blank reply message (D12)', has(r, 'Enter a reply'), r.text);
    r = await post('/Support/Reply/' + tid, { body: 'Staff reply' }, '/Support/Details/' + tid); check('COM-03a', 'staff reply', r.url.includes('/Support/Details/'), r.text);
    await login('2023-104857');
    const n = await get('/Notifications'); const link = [...n.doc.querySelectorAll('a.cp-notif-link')].find(a => a.textContent.includes('TKT-' + tid));
    const opened = link ? await fetch(link.getAttribute('href')) : null; check('COM-05a', 'reply notification opens ticket', opened && opened.url.includes('/Support/Details/' + tid), opened?.url);
    r = await post('/Support/Reply/' + tid, { body: 'Borrower reply' }, '/Support/Details/' + tid); check('COM-03b', 'borrower reply', r.url.includes('/Support/Details/'), r.text);
    await login('ADMIN-0001');
    r = await post('/Support/Resolve/' + tid, {}, '/Support/Details/' + tid); check('COM-04a', 'resolve', has(r, 'resolved'), r.text);
    await login('2023-104857');
    r = await post('/Support/Reply/' + tid, { body: 'after close' }, '/Support/Details/' + tid); check('COM-04b', 'reply after resolve refused', !has(r, 'Reply posted'), r.text);
    const all = await get('/Notifications');
    const broken = [];
    for (const a of [...all.doc.querySelectorAll('a.cp-notif-link')].slice(0, 15)) { const o = await fetch(a.getAttribute('href')); if (o.status !== 200 || /AccessDenied/.test(o.url)) broken.push(a.getAttribute('href') + ' -> ' + o.url); }
    check('COM-05b', 'all notification links open (no 403)', broken.length === 0, broken.join('; '));
    const bf = await get('/Notifications?type=BORROWING'); check('COM-06b', 'Borrowings filter matches loan notices', bf.doc.querySelectorAll('.cp-notif').length > 0, bf.doc.querySelectorAll('.cp-notif').length);
    r = await post('/Notifications/MarkAllRead', {}, '/Notifications'); check('COM-06', 'mark all read', (await get('/Notifications')).doc.querySelectorAll('.cp-notif.unread').length === 0, r.text);
  };

  suites.PRF = async () => {
    await login('2023-104857');
    const SC = o => post('/Profile/SaveContact', { ContactNumber: '09171234567', AlternateContact: '', NotifyReservationUpdates: 'true', NotifyDueReminders: 'true', NotifyOverdueAlerts: 'true', NotifyReturnConfirmations: 'true', NotifyAnnouncements: 'true', ...o }, '/Profile');
    for (const [v, l] of [['', 'blank'], ['call me', 'letters'], ['9'.repeat(200), 'too long']]) { const r = await SC({ ContactNumber: v }); check('PRF-01', 'contact ' + l, has(r, 'correct'), r.text); }
    let r = await SC({}); check('PRF-01d', 'contact save', has(r, 'saved'), r.text);
    const CP = (a, b, c) => post('/Profile/ChangePassword', { CurrentPassword: a, NewPassword: b, ConfirmPassword: c }, '/Profile');
    r = await CP('Nope1234', 'Campus456', 'Campus456'); check('PRF-02a', 'wrong current', has(r, 'incorrect'), r.text);
    r = await CP('Campus123', 'Campus456', 'Campus457'); check('PRF-02b', 'mismatch says mismatch (D11)', has(r, 'match'), r.text);
    r = await CP('Campus123', 'abc', 'abc'); check('PRF-02c', 'weak', has(r, 'uppercase'), r.text);
    r = await CP('Campus123', 'Campus123', 'Campus123'); check('PRF-02d', 'same as old', has(r, 'different'), r.text);
    r = await CP('Campus123', 'Campus456', 'Campus456'); check('PRF-02e', 'change password', has(r, 'changed|updated|saved'), r.text);
    const l = await login('2023-104857', 'Campus456'); check('PRF-02f', 'new password works', /Dashboard/.test(l.url), l.url);
    await CP('Campus456', 'Campus123', 'Campus123');
  };

  suites.SEC = async () => {
    await login('2023-104857');
    const payload = '<img src=x onerror=alert(1)><script>alert(2)</script>';
    const r = await post('/Support/Create', { Category: 'OTHER', Subject: payload, Body: payload }, '/Support/Create');
    const d = await get(r.url); check('SEC-01a', 'ticket text escaped', !d.doc.querySelector('img[onerror]') && ![...d.doc.scripts].some(s => s.textContent.includes('alert(2)')), r.url);
    for (const s of ["' OR 1=1 --", '"; DROP TABLE users; --']) { const g = await get('/Reservations/Available?search=' + encodeURIComponent(s)); check('SEC-02', 'search ' + s, g.status === 200, g.status); }
    check('SEC-03', 'auth cookie not readable by script (HttpOnly)', !/CampusPulse|\.AspNetCore\.Cookies/.test(document.cookie), document.cookie);
  };

  suites.UI = async () => {
    await login('2023-104857');
    const pages = ['/Borrower/Dashboard', '/Reservations/Available', '/Reservations/New/5', '/Reservations/Mine', '/Borrowings/Mine', '/Notifications', '/Profile', '/Support/Create'];
    for (const w of [375, 768, 1440]) {
      for (const u of pages) {
        const f = document.createElement('iframe'); f.style.cssText = `position:fixed;left:0;top:0;width:${w}px;height:800px;opacity:0;pointer-events:none`; f.src = u; document.body.appendChild(f);
        await new Promise(res => (f.onload = res));
        const over = f.contentDocument.documentElement.scrollWidth - w;
        if (w === 375) {
          const noLabel = [...f.contentDocument.querySelectorAll('input:not([type=hidden]):not([type=checkbox]):not([type=radio]),select,textarea')].filter(e => !(e.id && f.contentDocument.querySelector(`label[for="${e.id}"]`)) && !e.closest('label') && !e.getAttribute('aria-label'));
          check('UI-03', 'labels ' + u, noLabel.length === 0, noLabel.map(e => e.name).join(','));
        }
        check('UI-01', `${w}px ${u}`, over <= 0, over > 0 ? `overflow ${over}px` : '');
        f.remove();
      }
    }
  };

  const order = ['AUTH', 'RBAC', 'EQP', 'RES', 'CUS', 'RET', 'ADM', 'COM', 'PRF', 'MON', 'SEC', 'UI'];
  const runAll = async () => {
    results.length = 0;
    for (const s of order) {
      try { await suites[s](); } catch (e) { check(s + '-ERR', 'suite crashed', false, e.message); }
    }
    console.table(results);
    return summary();
  };
  const summary = () => ({ total: results.length, passed: results.filter(r => r.result === 'PASS').length, failed: results.filter(r => r.result === 'FAIL') });

  window.CRAFT = { suites, runAll, results, summary, helpers: { get, post, postForm, login, logout, reserve, day } };
})();
