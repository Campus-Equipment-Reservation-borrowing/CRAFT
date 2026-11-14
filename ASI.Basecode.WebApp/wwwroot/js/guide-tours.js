/*
 * Walkthrough content per role (STUDENT and FACULTY share the borrower tours).
 * Targets use stable hooks: route links, form actions and field names.
 * See guide.js for the step fields.
 */
(function () {
    'use strict';

    var nav = function (href) { return '#appSidebar a.nav-item[href="' + href + '"]'; };
    var BELL = '.craft-header a.icon-btn[href="/Notifications"]';
    var PROFILE = '.craft-header a.user[href="/Profile"]';
    var GUIDE = '[data-guide-launcher]';

    var borrower = [
        {
            id: 'borrower-overview', overview: true,
            title: 'Welcome to CRAFT', description: 'A quick look around your dashboard',
            steps: [
                { page: '/Borrower/Dashboard', url: '/Borrower/Dashboard', title: 'Welcome to CRAFT', text: 'CRAFT is how you borrow equipment from the Property Office. This short tour shows where everything is. You can skip it and replay it any time.' },
                { page: '/Borrower/Dashboard', target: '#appSidebar', title: 'Main menu', text: 'Everything you can do is in this menu: browse equipment, follow your reservations and borrowings, and ask for help.' },
                { page: '/Borrower/Dashboard', target: '.cp-kpi-grid', title: 'At a glance', text: 'Your pending requests, items ready for pickup, active borrowings and anything overdue. Click a card to open that list.' },
                { page: '/Borrower/Dashboard', target: nav('/Reservations/Available'), title: 'Browse equipment', text: 'Start here to find equipment and send a reservation request.' },
                { page: '/Borrower/Dashboard', target: nav('/Reservations/Mine'), title: 'My Reservations', text: 'Follow each request from Pending to Approved, print the pickup slip, or cancel while it is still pending or approved.' },
                { page: '/Borrower/Dashboard', target: nav('/Borrowings/Mine'), title: 'My Borrowings', text: 'Items you have right now and their due dates. Overdue items block new reservations until they are returned.' },
                { page: '/Borrower/Dashboard', target: BELL, title: 'Notifications', text: 'Approvals, pickup and return updates, due-date reminders and replies to your tickets arrive here. Click one to open what it is about.' },
                { page: '/Borrower/Dashboard', target: PROFILE, title: 'Profile & settings', text: 'Update your contact number, choose which notifications you get, and change your password.' },
                { page: '/Borrower/Dashboard', target: GUIDE, title: 'Need a refresher?', text: 'Open this Guide button for step-by-step tours, such as how to reserve equipment.' }
            ]
        },
        {
            id: 'borrower-reserve',
            title: 'Reserve equipment', description: 'From browsing to your pickup slip',
            steps: [
                { page: '/Reservations/Available', url: '/Reservations/Available', target: 'input[name="search"]', title: 'Find equipment', text: 'Search by name or asset code. The category filter next to it narrows the list.' },
                { page: '/Reservations/Available', target: 'a.eq-card:not(.off)', click: true, title: 'Pick an item', text: 'Each card shows how many units are available. Greyed-out cards are out of stock or under maintenance.', note: 'No equipment is available right now.' },
                { page: '/Reservations/EquipmentDetails', target: 'a[href^="/Reservations/New/"]', click: true, title: 'Check the details', text: 'Review the condition, location and borrowing rules, then click Reserve Equipment.', note: 'Open an equipment item to continue.' },
                { page: '/Reservations/New', target: '[name="Form.Quantity"]', title: 'How many units', text: 'The limit shown is the most one borrower may request, capped by what is available.', note: 'Open the reservation form for an item to continue.' },
                { page: '/Reservations/New', target: '[name="Form.PickupLocationId"]', title: 'Pickup point', text: 'Choose where you will collect the equipment.' },
                { page: '/Reservations/New', target: '[name="Form.BorrowingDate"]', title: 'Schedule', text: 'Pick when you need it and when you will return it. Requests need at least 1 day of notice and can be made up to 30 days ahead; the policy limits how long you can borrow.' },
                { page: '/Reservations/New', target: '[name="Form.Purpose"]', title: 'Purpose', text: 'Be specific: the custodian uses this to prioritise competing requests.' },
                { page: '/Reservations/New', target: '[name="Form.AcknowledgeRules"]', title: 'Acknowledgements', text: 'Tick both boxes to accept the borrowing rules and responsibility for overdue items.' },
                { page: '/Reservations/New', target: 'form[action^="/Reservations/New"] button[type="submit"]', title: 'Submit', text: 'Your request goes to a custodian for review. You will be notified when it is approved or rejected.' },
                { page: '/Reservations/Mine', url: '/Reservations/Mine', target: 'table.cp-table, .cp-empty-state', title: 'Follow your request', text: 'Open View for details and the pickup slip. Cancel is available while a request is pending or approved.' }
            ]
        },
        {
            id: 'borrower-borrowings',
            title: 'Borrowings & returns', description: 'Due dates, overdue items and history',
            steps: [
                { page: '/Borrowings/Mine', url: '/Borrowings/Mine', target: 'table.cp-table, .cp-empty-state', title: 'What you have now', text: 'Each released item shows its due date. Bring it back to the Property Office on time; overdue items block new reservations.' },
                { page: '/Borrowings/Mine', target: BELL, title: 'Reminders', text: 'You get a reminder a day before an item is due, and a notice if it becomes overdue.' },
                { page: '/Borrowings/History', url: '/Borrowings/History', target: 'table.cp-table, .cp-empty-state', title: 'History', text: 'Returned items, their condition at inspection, and any penalties recorded.' }
            ]
        },
        {
            id: 'borrower-support',
            title: 'Get help', description: 'Open a support ticket or appeal',
            steps: [
                { page: '/Support', url: '/Support', target: 'a[href="/Support/Create"]', click: true, title: 'New ticket', text: 'Ask a question, report a problem, or appeal a decision.' },
                { page: '/Support/Create', url: '/Support/Create', target: 'input[name="Category"]', title: 'Choose a category', text: 'Use Appeal to ask the office to reconsider a rejected verification, reservation or penalty.' },
                { page: '/Support/Create', target: '[name="Subject"]', title: 'Describe it', text: 'Add a short subject and the details. Replies appear on the ticket and in your notifications.' }
            ]
        }
    ];

    var custodian = [
        {
            id: 'custodian-overview', overview: true,
            title: 'Welcome, custodian', description: 'Your console at a glance',
            steps: [
                { page: '/Custodian/Dashboard', url: '/Custodian/Dashboard', title: 'Welcome to the custodian console', text: 'You review reservation requests, release equipment, and record returns and inspections. This tour shows where each task lives.' },
                { page: '/Custodian/Dashboard', target: '.cp-kpi-grid', title: 'At a glance', text: 'Pending requests, items ready to release, active loans and overdue items. Click a card to jump to that list.' },
                { page: '/Custodian/Dashboard', target: nav('/Custodian/Reservations'), title: 'Reservation queue', text: 'Requests routed to you, plus unassigned ones you can claim. Review, approve or reject them here.' },
                { page: '/Custodian/Dashboard', target: nav('/Custodian/Scan'), title: 'Scan', text: 'Scan or type an asset code or reservation number to jump straight to its release or return.' },
                { page: '/Custodian/Dashboard', target: nav('/Custodian/Borrowings'), title: 'Active borrowings', text: 'Everything currently out, with due dates and a Record return button.' },
                { page: '/Custodian/Dashboard', target: nav('/Returns'), title: 'Returns & inspection', text: 'Returned items wait here for an inspection that puts them back into stock, or into maintenance.' },
                { page: '/Custodian/Dashboard', target: nav('/Equipment'), title: 'Equipment', text: 'Add items, update quantities and condition, and print asset labels.' },
                { page: '/Custodian/Dashboard', target: BELL, title: 'Notifications', text: 'New requests, overdue loans, damage reports and low-stock alerts. Click one to open it.' },
                { page: '/Custodian/Dashboard', target: GUIDE, title: 'Task tours', text: 'Open the Guide button for step-by-step tours of reviewing, releasing and returning.' }
            ]
        },
        {
            id: 'custodian-review',
            title: 'Review a request', description: 'Approve or reject reservations',
            steps: [
                { page: '/Custodian/Reservations', url: '/Custodian/Reservations', target: 'input[name="search"]', title: 'Find requests', text: 'Search by borrower, equipment or reservation number, and filter by status.' },
                { page: '/Custodian/Reservations', target: 'a[href^="/Custodian/Review/"]', click: true, title: 'Open a request', text: 'Review shows the borrower, schedule, purpose and stock.', note: 'There are no requests in the queue right now.' },
                { page: '/Custodian/Review', target: 'form[action^="/Custodian/Claim"]', title: 'Claim unassigned requests', text: 'Requests with no custodian can be claimed so they are routed to you.', note: 'This request is already assigned.' },
                { page: '/Custodian/Review', target: 'form[action^="/Custodian/Approve"]', title: 'Approve', text: 'Approving holds the units for pickup and notifies the borrower. Notes are optional (up to 500 characters).', note: 'Only pending requests can be approved.' },
                { page: '/Custodian/Review', target: 'form[action^="/Custodian/Reject"]', title: 'Reject', text: 'A reason is required; the borrower sees it and the held units are released.' }
            ]
        },
        {
            id: 'custodian-release',
            title: 'Release equipment', description: 'Hand items to a borrower',
            steps: [
                { page: '/Custodian/Scan', url: '/Custodian/Scan', target: 'input[name="code"]', title: 'Scan the slip or label', text: 'Scan the reservation number on the pickup slip (or type it). An approved reservation opens its release form directly.' },
                { page: '/Custodian/Release', target: '[name="ReleasedQuantity"]', title: 'Quantity', text: 'Release all approved units, or fewer for a partial release.', note: 'Open the release form from Scan or the queue to continue.' },
                { page: '/Custodian/Release', target: '[name="ReleaseConditionId"]', title: 'Condition at release', text: 'Record the condition the item leaves in. Damaged, lost or under-maintenance items cannot be released.' },
                { page: '/Custodian/Borrowings', url: '/Custodian/Borrowings', target: 'table.cp-table, .cp-empty-state', title: 'Now on loan', text: 'Released items appear in Active Borrowings until they are returned.' }
            ]
        },
        {
            id: 'custodian-return',
            title: 'Return & inspect', description: 'Record returns and condition',
            steps: [
                { page: '/Custodian/Borrowings', url: '/Custodian/Borrowings', target: 'a[href^="/Returns/Create/"]', click: true, title: 'Record a return', text: 'Click Record return on the borrowing being brought back (scanning the asset code also opens it).', note: 'Nothing is on loan right now.' },
                { page: '/Returns/Create', target: '[name="ReturnedQuantity"]', title: 'Units returned', text: 'Enter how many units came back; partial returns keep the loan open for the rest.' },
                { page: '/Returns/Create', target: '[name="ReturnedAtUtc"]', title: 'Return time', text: 'Defaults to now. Late returns are flagged with the days overdue.' },
                { page: '/Returns', url: '/Returns', target: 'a[href^="/Returns/Inspect/"]', title: 'Inspect', text: 'Each return needs an inspection: good units go back into stock, damaged units go to maintenance, lost units are reconciled.', note: 'No returns are waiting for inspection.' }
            ]
        },
        {
            id: 'custodian-equipment',
            title: 'Manage equipment', description: 'Add items and print labels',
            steps: [
                { page: '/Equipment', url: '/Equipment', target: 'a[href="/Equipment/Create"]', title: 'Add equipment', text: 'Create an item with its asset code, category, location, quantity and photo.' },
                { page: '/Equipment', target: 'input[name="search"]', title: 'Find and filter', text: 'Search and filter by category, location, condition or status. Open an item to edit it or print its label.' }
            ]
        }
    ];

    var admin = [
        {
            id: 'admin-overview', overview: true,
            title: 'Welcome, administrator', description: 'The admin console at a glance',
            steps: [
                { page: '/Monitoring/Dashboard', url: '/Monitoring/Dashboard', title: 'Welcome to the admin console', text: 'You manage accounts, custodians, borrowing rules and reports. This tour shows where each lives.' },
                { page: '/Monitoring/Dashboard', target: '.cp-kpi-grid', title: 'Monitoring', text: 'Users, stock and transaction figures across the system. Click a card for the detail.' },
                { page: '/Monitoring/Dashboard', target: nav('/Users/Verifications'), title: 'Verifications', text: 'New student and faculty accounts wait here until you verify them.' },
                { page: '/Monitoring/Dashboard', target: nav('/Users'), title: 'Users', text: 'Create staff accounts, edit roles and status, and reset passwords.' },
                { page: '/Monitoring/Dashboard', target: nav('/CustodianAssignments'), title: 'Custodian assignments', text: 'Route requests to custodians by borrower, location or category.' },
                { page: '/Monitoring/Dashboard', target: nav('/Settings'), title: 'System settings', text: 'Borrowing policy (limits and durations), equipment categories and locations.' },
                { page: '/Monitoring/Dashboard', target: nav('/Announcements'), title: 'Announcements', text: 'Post notices to students, faculty or custodians.' },
                { page: '/Monitoring/Dashboard', target: nav('/Monitoring/Reports'), title: 'Reports', text: 'Summaries and CSV exports of reservations, borrowings, movements and the audit log.' },
                { page: '/Monitoring/Dashboard', target: GUIDE, title: 'Task tours', text: 'Open the Guide button for step-by-step tours of each admin task.' }
            ]
        },
        {
            id: 'admin-verify',
            title: 'Verify accounts', description: 'Approve or reject new sign-ups',
            steps: [
                { page: '/Users/Verifications', url: '/Users/Verifications', target: 'form[action^="/Users/Verify/"]', title: 'Verify', text: 'Check the ID and email, then Verify. Verified users can start reserving equipment.', note: 'No accounts are waiting for verification.' },
                { page: '/Users/Verifications', target: 'textarea[name="reason"]', title: 'Reject with a reason', text: 'A reason is required. The user is notified and can appeal through a support ticket.' }
            ]
        },
        {
            id: 'admin-users',
            title: 'Manage users', description: 'Create accounts and reset passwords',
            steps: [
                { page: '/Users', url: '/Users', target: 'a[href="/Users/Create"]', title: 'Add a user', text: 'Create staff or borrower accounts with a temporary password; the user must change it at first sign-in.' },
                { page: '/Users', target: 'input[name="search"]', title: 'Find users', text: 'Search and filter by role, department or status. Open a user to edit, view history, reset the password or create an invite link.' }
            ]
        },
        {
            id: 'admin-assignments',
            title: 'Assign custodians', description: 'Route requests to the right person',
            steps: [
                { page: '/CustodianAssignments', url: '/CustodianAssignments', target: 'form[action$="/CustodianAssignments/Assign"]', title: 'Create an assignment', text: 'Assign a custodian to a borrower, a location or a category. New requests that match are routed to them; others stay in the shared pool.' }
            ]
        },
        {
            id: 'admin-settings',
            title: 'Borrowing policy & lists', description: 'Limits, categories and locations',
            steps: [
                { page: '/Settings', url: '/Settings', target: '#policy', title: 'Borrowing policy', text: 'Set the maximum borrowing days, units per reservation, open requests per borrower, overdue grace and low-stock threshold. Publishing creates a new version; history is kept.' },
                { page: '/Settings', target: '#categories', title: 'Equipment categories', text: 'Add categories with a short code (e.g. LAPTOP). Categories in use cannot be deleted.' },
                { page: '/Settings', target: '#locations', title: 'Locations', text: 'Storage and pickup locations. Mark a location as a pickup point to offer it on the reservation form.' }
            ]
        },
        {
            id: 'admin-announcements',
            title: 'Announcements & reports', description: 'Notices and CSV exports',
            steps: [
                { page: '/Announcements', url: '/Announcements', target: 'input[name="TargetRoles"]', title: 'Choose the audience', text: 'Pick who sees the announcement. Each recipient also gets a notification.' },
                { page: '/Announcements', target: '#annExpires', title: 'Expiry', text: 'Optional: the banner disappears after this date.' },
                { page: '/Monitoring/Reports', url: '/Monitoring/Reports', target: 'a[href="/Monitoring/ExportReservationsCsv"]', title: 'Exports', text: 'Download CSV files of reservations, borrowings, inventory movements and the audit log.' }
            ]
        }
    ];

    window.CRAFT_TOURS = { STUDENT: borrower, FACULTY: borrower, TECHNICIAN: custodian, ADMIN: admin };
})();
